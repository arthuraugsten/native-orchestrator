using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NativeOrchestrator;

/// <summary>
/// The hosted service that claims due sagas and runs them, up to <see cref="NativeOrchestratorOptions.MaxConcurrency"/>
/// at once. Any number of instances can run it against the same store.
/// </summary>
internal sealed class SagaWorker : BackgroundService
{
  private readonly ISagaWorkQueue _queue;
  private readonly SagaEngine _engine;
  private readonly ISagaStoreInitializer[] _initializers;
  private readonly SagaWorkSignal _signal;
  private readonly NativeOrchestratorOptions _options;
  private readonly TimeProvider _timeProvider;
  private readonly ILogger<SagaWorker> _logger;
  private readonly Lock _runningLock = new();
  private readonly HashSet<Task> _running = [];

  public SagaWorker(
    SagaRegistry registry,
    ISagaWorkQueue queue,
    SagaEngine engine,
    IEnumerable<ISagaStoreInitializer> initializers,
    SagaWorkSignal signal,
    IOptions<NativeOrchestratorOptions> options,
    TimeProvider timeProvider,
    ILogger<SagaWorker> logger)
  {
    // The registry is a dependency so that invalid saga declarations fail at startup, when the worker is created.
    ArgumentNullException.ThrowIfNull(registry);
    _queue = queue;
    _engine = engine;
    _initializers = initializers.ToArray();
    _signal = signal;
    _options = options.Value;
    _timeProvider = timeProvider;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (!_options.WorkerEnabled)
    {
      SagaLog.WorkerDisabled(_logger);
      return;
    }

    foreach (var initializer in _initializers)
      await initializer.InitializeAsync(stoppingToken).ConfigureAwait(false);

    SagaLog.WorkerStarted(_logger, _options.InstanceName, _options.MaxConcurrency);
    using var slots = new SemaphoreSlim(_options.MaxConcurrency, _options.MaxConcurrency);
    try
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        await slots.WaitAsync(stoppingToken).ConfigureAwait(false);
        var free = 1;
        while (free < _options.MaxConcurrency && slots.Wait(0))
          free++;

        IReadOnlyList<ClaimedSaga> claimed;
        try
        {
          var now = _timeProvider.GetUtcNow();
          claimed = await _queue.ClaimDueAsync(new SagaClaimRequest(_options.InstanceName, free, now, now + _options.LeaseDuration), stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
          slots.Release(free);
          SagaLog.ClaimFailed(_logger, ex, _options.ScanInterval);
          await _signal.WaitAsync(_options.ScanInterval, _timeProvider, stoppingToken).ConfigureAwait(false);
          continue;
        }

        if (claimed.Count < free)
          slots.Release(free - claimed.Count);
        foreach (var saga in claimed)
          Track(RunAsync(saga, slots, stoppingToken));

        if (claimed.Count < free)
          await _signal.WaitAsync(_options.ScanInterval, _timeProvider, stoppingToken).ConfigureAwait(false);
      }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
      // Shutting down.
    }

    Task[] running;
    lock (_runningLock)
      running = [.. _running];
    await Task.WhenAll(running).ConfigureAwait(false);
  }

  private async Task RunAsync(ClaimedSaga saga, SemaphoreSlim slots, CancellationToken stoppingToken)
  {
    try
    {
      await Task.Yield();
      await _engine.RunAsync(saga, stoppingToken).ConfigureAwait(false);
    }
    finally
    {
      slots.Release();
    }
  }

  private void Track(Task task)
  {
    lock (_runningLock)
      _running.Add(task);
    _ = task.ContinueWith(
      completed =>
      {
        lock (_runningLock)
          _running.Remove(completed);
      },
      CancellationToken.None,
      TaskContinuationOptions.ExecuteSynchronously,
      TaskScheduler.Default);
  }
}
