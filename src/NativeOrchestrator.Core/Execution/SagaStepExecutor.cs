using Microsoft.Extensions.Logging;

namespace NativeOrchestrator;

/// <summary>Runs a saga's current step and applies the result: next step, poll, retry, or failure.</summary>
internal sealed class SagaStepExecutor
{
  private readonly IRetryPolicy _retryPolicy;
  private readonly IBackoffStrategy _backoff;
  private readonly SagaDiagnostics _diagnostics;
  private readonly TimeProvider _timeProvider;
  private readonly ILogger<SagaStepExecutor> _logger;

  public SagaStepExecutor(
    IRetryPolicy retryPolicy,
    IBackoffStrategy backoff,
    SagaDiagnostics diagnostics,
    TimeProvider timeProvider,
    ILogger<SagaStepExecutor> logger)
  {
    _retryPolicy = retryPolicy;
    _backoff = backoff;
    _diagnostics = diagnostics;
    _timeProvider = timeProvider;
    _logger = logger;
  }

  public async Task<RunOutcome> RunCurrentStepAsync(SagaRun run, CancellationToken runToken)
  {
    var saga = run.Saga;
    var definition = run.Runtime.Steps[saga.CurrentStepIndex];
    var options = EffectiveStepOptions.Resolve(run.Options, definition);

    var step = saga.FindStep(definition.Index);
    if (step is null)
    {
      step = new StepRecord
      {
        StepIndex = definition.Index,
        Name = definition.Name,
        Status = StepStatus.InProgress,
        StartedAtUtc = run.Now,
      };
      saga.Steps.Add(step);
      run.Record(SagaEventType.StepStarted, step.Name);
    }

    saga.Status = SagaStatus.Running;
    saga.NextRunAtUtc = run.Now;

    var deadline = step.StartedAtUtc + options.Timeout;
    if (run.Now >= deadline)
      return FailStep(run, step, options, SagaFailureKind.StepTimedOut, $"Step '{step.Name}' didn't finish within its timeout of {options.Timeout}.");

    StepResult? result = null;
    var started = _timeProvider.GetTimestamp();
    var error = await StepInvoker.InvokeAsync(
      async token => result = await run.Runtime.ExecuteStepAsync(saga, step, definition, run.Services, token).ConfigureAwait(false),
      options.ExecutionTimeout,
      _timeProvider,
      runToken).ConfigureAwait(false);
    var elapsed = _timeProvider.GetElapsedTime(started);

    if (error is StepFailedException failed)
    {
      result = StepResult.Failure(failed.Message);
      error = null;
    }

    if (error is not null)
    {
      _diagnostics.RecordStep(saga.SagaType, step.Name, "error", elapsed);
      return RetryOrFail(run, step, options, error, deadline);
    }

    switch (result!.Kind)
    {
      case StepResultKind.Success:
        _diagnostics.RecordStep(saga.SagaType, step.Name, "succeeded", elapsed);
        step.Status = StepStatus.Succeeded;
        step.CompletedAtUtc = run.Now;
        step.Attempt = 0;
        step.Error = null;
        saga.CurrentStepIndex++;
        run.Record(SagaEventType.StepSucceeded, step.Name);
        return RunOutcome.Continue;

      case StepResultKind.Poll:
        _diagnostics.RecordStep(saga.SagaType, step.Name, "polled", elapsed);
        step.Attempt = 0;
        step.Error = null;
        step.CheckCount++;
        if (step.CheckCount == 1)
          run.Record(SagaEventType.StepWaiting, step.Name);
        var delay = result.Delay is { } requested
          ? Min(requested, options.Polling.MaxDelay)
          : _backoff.GetDelay(step.CheckCount, options.Polling);
        run.WaitUntil(Min(run.Now + delay, deadline));
        return RunOutcome.Wait;

      default:
        _diagnostics.RecordStep(saga.SagaType, step.Name, "failed", elapsed);
        return FailStep(run, step, options, SagaFailureKind.StepFailed, result.Error!);
    }
  }

  private RunOutcome RetryOrFail(SagaRun run, StepRecord step, EffectiveStepOptions options, Exception error, DateTimeOffset deadline)
  {
    var saga = run.Saga;
    step.Attempt++;
    step.Error = run.Truncate(error.Message);
    var decision = _retryPolicy.Evaluate(new RetryContext(saga.SagaType, step.Name, step.Attempt, error, options.Retry, false));
    if (!decision.ShouldRetry)
      return FailStep(run, step, options, SagaFailureKind.StepFailed, $"Step '{step.Name}' failed after {step.Attempt} attempts: {error.Message}");

    SagaLog.StepRetrying(_logger, error, step.Name, saga.Id, step.Attempt, decision.Delay);
    run.Record(SagaEventType.StepRetryScheduled, step.Name, run.Truncate($"Attempt {step.Attempt} failed: {error.Message}"));
    run.WaitUntil(Min(run.Now + decision.Delay, deadline));
    return RunOutcome.Wait;
  }

  private RunOutcome FailStep(SagaRun run, StepRecord step, EffectiveStepOptions options, SagaFailureKind kind, string error)
  {
    error = run.Truncate(error);
    step.Status = StepStatus.Failed;
    step.CompletedAtUtc = run.Now;
    step.Error = error;
    run.Record(kind == SagaFailureKind.StepTimedOut ? SagaEventType.StepTimedOut : SagaEventType.StepFailed, step.Name, error);
    SagaLog.StepFailed(_logger, step.Name, run.Saga.Id, error);

    if (options.ContinueOnError)
      run.Saga.CurrentStepIndex++;
    else
      run.BeginCompensation(kind, step.Name, error);
    return RunOutcome.Continue;
  }

  private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

  private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
