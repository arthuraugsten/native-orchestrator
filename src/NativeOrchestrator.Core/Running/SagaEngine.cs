using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NativeOrchestrator;

/// <summary>
/// Runs a claimed saga as far as it can go now: steps until one has to wait (a poll or a retry), compensation, or the
/// end. Saves after every step, so a crash repeats at most the step that was running.
/// </summary>
internal sealed class SagaEngine
{
  private readonly SagaRegistry _registry;
  private readonly ISagaWorkQueue _queue;
  private readonly SagaStepExecutor _steps;
  private readonly CompensationRunner _compensation;
  private readonly IServiceScopeFactory _scopes;
  private readonly IOptionsMonitor<SagaOptions> _sagaOptions;
  private readonly IOptionsMonitor<SagaOrchestratorOptions> _options;
  private readonly ISagaLifecycleObserver[] _observers;
  private readonly SagaDiagnostics _diagnostics;
  private readonly TimeProvider _timeProvider;
  private readonly ILogger<SagaEngine> _logger;

  public SagaEngine(
    SagaRegistry registry,
    ISagaWorkQueue queue,
    SagaStepExecutor steps,
    CompensationRunner compensation,
    IServiceScopeFactory scopes,
    IOptionsMonitor<SagaOptions> sagaOptions,
    IOptionsMonitor<SagaOrchestratorOptions> options,
    IEnumerable<ISagaLifecycleObserver> observers,
    SagaDiagnostics diagnostics,
    TimeProvider timeProvider,
    ILogger<SagaEngine> logger)
  {
    _registry = registry;
    _queue = queue;
    _steps = steps;
    _compensation = compensation;
    _scopes = scopes;
    _sagaOptions = sagaOptions;
    _options = options;
    _observers = [.. observers];
    _diagnostics = diagnostics;
    _timeProvider = timeProvider;
    _logger = logger;
  }

  public async Task RunAsync(ClaimedSaga claimed, CancellationToken stoppingToken)
  {
    var (saga, lease) = claimed;
    var options = _options.CurrentValue;
    if (!_registry.TryGet(saga.SagaType, out var runtime))
    {
      await PostponeUnknownSagaTypeAsync(saga, lease, options).ConfigureAwait(false);
      return;
    }

    using var activity = SagaDiagnostics.StartRun(saga);
    await using var scope = _scopes.CreateAsyncScope();
    var run = new SagaRun(saga, runtime, _sagaOptions.Get(saga.SagaType), scope.ServiceProvider, _timeProvider, options.MaxErrorLength, _logger);
    var saved = new List<SagaHistoryEntry>();
    var finished = false;

    var keeper = new LeaseKeeper(_queue, lease, options, _timeProvider, _logger, stoppingToken);
    await using (keeper.ConfigureAwait(false))
    {
      try
      {
        await AdvanceAsync(run, lease, saved, keeper.Token).ConfigureAwait(false);
        await keeper.StopAsync().ConfigureAwait(false);
        saga.MarkUpdated(run.Now);
        // The final save isn't cancelled by shutdown, so finished work isn't thrown away; the store's command timeout bounds it.
        await _queue.SaveAsync(lease, saga, run.PendingHistory, releaseLease: true, CancellationToken.None).ConfigureAwait(false);
        saved.AddRange(run.PendingHistory);
        finished = saga.Status.IsFinished();
      }
      catch (SagaConcurrencyException ex)
      {
        SagaLog.ConcurrencyConflict(_logger, saga.Id, ex.Message);

        await TryReleaseAsync(lease).ConfigureAwait(false);
      }
      catch (OperationCanceledException) when (keeper.Token.IsCancellationRequested)
      {
        if (stoppingToken.IsCancellationRequested)
          SagaLog.RunAborted(_logger, saga.Id);

        await TryReleaseAsync(lease).ConfigureAwait(false);
      }
      catch (Exception ex)
      {
        // An engine or store failure. Keep the lease: the saga runs again once it expires, which backs off a repeated failure.
        SagaLog.RunFailed(_logger, ex, saga.Id, saga.SagaType);
      }
    }

    await PublishAsync(saved).ConfigureAwait(false);

    if (!finished)
      return;

    _diagnostics.RecordFinished(saga.SagaType, saga.Status);

    SagaLog.SagaFinished(_logger, saga.Id, saga.SagaType, saga.Status);

    try
    {
      await runtime.OnFinishedAsync(saga, scope.ServiceProvider, CancellationToken.None).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      SagaLog.HookFailed(_logger, ex, saga.Id, saga.SagaType);
    }
  }

  private async Task AdvanceAsync(SagaRun run, SagaLease lease, List<SagaHistoryEntry> saved, CancellationToken runToken)
  {
    var saga = run.Saga;
    if (saga.Start(run.Now))
      run.Record(SagaEventType.Started);

    while (true)
    {
      if (saga.Status is SagaStatus.Running or SagaStatus.Waiting)
      {
        if (saga.CancellationRequested)
          run.BeginCompensation(SagaFailureKind.Cancelled, null, "Cancellation was requested.");

        else if (run.Now - saga.StartedAtUtc >= run.Options.MaxDuration)
          run.BeginCompensation(SagaFailureKind.SagaTimedOut, null, $"The saga didn't finish within its maximum duration of {run.Options.MaxDuration}.");

        else if (saga.CurrentStepIndex >= run.Runtime.Steps.Count)
        {
          run.Finish(SagaStatus.Completed);
          return;
        }
        else if (await _steps.RunCurrentStepAsync(run, runToken).ConfigureAwait(false) == RunOutcome.Wait)
          return;
      }
      else if (saga.Status == SagaStatus.Compensating)
      {
        var outcome = await _compensation.CompensateNextAsync(run, runToken).ConfigureAwait(false);

        if (outcome == RunOutcome.Wait)
          return;

        if (outcome == RunOutcome.Done)
        {
          run.Finish(saga.AnyCompensationFailed() ? SagaStatus.Failed : SagaStatus.Compensated);
          return;
        }
      }
      else
      {
        return;
      }

      // Checkpoint: the step's result is stored before the next step runs.
      saga.MarkUpdated(run.Now);

      await _queue.SaveAsync(lease, saga, run.PendingHistory, releaseLease: false, runToken).ConfigureAwait(false);

      saved.AddRange(run.PendingHistory);
      run.PendingHistory.Clear();
    }
  }

  private async Task PostponeUnknownSagaTypeAsync(SagaRecord saga, SagaLease lease, SagaOrchestratorOptions options)
  {
    SagaLog.UnknownSagaType(_logger, saga.Id, saga.SagaType, options.UnknownSagaTypeRetryDelay);
    saga.ScheduleAt(_timeProvider.GetUtcNow() + options.UnknownSagaTypeRetryDelay);
    try
    {
      await _queue.SaveAsync(lease, saga, [], releaseLease: true, CancellationToken.None).ConfigureAwait(false);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      SagaLog.RunFailed(_logger, ex, saga.Id, saga.SagaType);
    }
  }

  private async Task TryReleaseAsync(SagaLease lease)
  {
    try
    {
      await _queue.ReleaseLeaseAsync(lease, CancellationToken.None).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
      SagaLog.LeaseReleaseFailed(_logger, ex, lease.SagaId);
    }
  }

  private async Task PublishAsync(List<SagaHistoryEntry> entries)
  {
    if (_observers.Length == 0)
      return;
    foreach (var entry in entries)
    {
      foreach (var observer in _observers)
      {
        try
        {
          await observer.OnEventAsync(entry, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
          SagaLog.ObserverFailed(_logger, ex, observer.GetType().Name, entry.EventType, entry.SagaId);
        }
      }
    }
  }
}
