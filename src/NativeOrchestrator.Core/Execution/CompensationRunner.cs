using Microsoft.Extensions.Logging;

namespace NativeOrchestrator;

/// <summary>Compensates a failed saga's succeeded steps, latest first, one per call.</summary>
internal sealed class CompensationRunner
{
  private readonly IRetryPolicy _retryPolicy;
  private readonly SagaDiagnostics _diagnostics;
  private readonly TimeProvider _timeProvider;
  private readonly ILogger<CompensationRunner> _logger;

  public CompensationRunner(
    IRetryPolicy retryPolicy,
    SagaDiagnostics diagnostics,
    TimeProvider timeProvider,
    ILogger<CompensationRunner> logger)
  {
    _retryPolicy = retryPolicy;
    _diagnostics = diagnostics;
    _timeProvider = timeProvider;
    _logger = logger;
  }

  public async Task<RunOutcome> CompensateNextAsync(SagaRun run, CancellationToken runToken)
  {
    var saga = run.Saga;
    var step = LatestSucceededStep(saga);
    if (step is null)
      return RunOutcome.Done;

    saga.NextRunAtUtc = run.Now;
    var definition = run.Runtime.FindStep(step.Name);
    if (definition is null)
    {
      MarkFailed(run, step, $"Step '{step.Name}' is no longer declared by saga '{saga.SagaType}', so it can't be compensated.");
      return RunOutcome.Continue;
    }

    var options = EffectiveStepOptions.Resolve(run.Options, definition);
    var started = _timeProvider.GetTimestamp();
    var error = await StepInvoker.InvokeAsync(
      token => run.Runtime.CompensateStepAsync(saga, step, definition, run.Services, token),
      options.ExecutionTimeout,
      _timeProvider,
      runToken).ConfigureAwait(false);
    var elapsed = _timeProvider.GetElapsedTime(started);

    if (error is null)
    {
      _diagnostics.RecordStep(saga.SagaType, step.Name, "compensated", elapsed);
      step.Status = StepStatus.Compensated;
      step.CompensationAttempt = 0;
      run.Record(SagaEventType.StepCompensated, step.Name);
      return RunOutcome.Continue;
    }

    _diagnostics.RecordStep(saga.SagaType, step.Name, "compensation-error", elapsed);
    step.CompensationAttempt++;
    step.Error = run.Truncate(error.Message);
    SagaLog.CompensationAttemptFailed(_logger, error, step.Name, saga.Id, step.CompensationAttempt);

    var decision = error is StepFailedException
      ? RetryDecision.Stop
      : _retryPolicy.Evaluate(new RetryContext(saga.SagaType, step.Name, step.CompensationAttempt, error, options.CompensationRetry, true));
    if (decision.ShouldRetry)
    {
      run.Record(SagaEventType.CompensationRetryScheduled, step.Name, run.Truncate($"Attempt {step.CompensationAttempt} failed: {error.Message}"));
      run.WaitUntil(run.Now + decision.Delay);
      return RunOutcome.Wait;
    }

    MarkFailed(run, step, $"Compensating step '{step.Name}' failed after {step.CompensationAttempt} attempts: {error.Message}");
    return RunOutcome.Continue;
  }

  public static bool AnyCompensationFailed(SagaRecord saga)
  {
    foreach (var step in saga.Steps)
    {
      if (step.Status == StepStatus.CompensationFailed)
        return true;
    }

    return false;
  }

  private void MarkFailed(SagaRun run, StepRecord step, string error)
  {
    step.Status = StepStatus.CompensationFailed;
    step.Error = run.Truncate(error);
    run.Record(SagaEventType.StepCompensationFailed, step.Name, step.Error);
    SagaLog.CompensationFailed(_logger, step.Name, run.Saga.Id);
  }

  private static StepRecord? LatestSucceededStep(SagaRecord saga)
  {
    StepRecord? latest = null;
    foreach (var step in saga.Steps)
    {
      if (step.Status == StepStatus.Succeeded && (latest is null || step.StepIndex > latest.StepIndex))
        latest = step;
    }

    return latest;
  }
}
