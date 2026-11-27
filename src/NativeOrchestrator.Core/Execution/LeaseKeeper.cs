using Microsoft.Extensions.Logging;

namespace NativeOrchestrator;

/// <summary>
/// Renews a saga's lease on a timer while the saga runs. If the lease is lost, it cancels <see cref="Token"/>, so the
/// run stops and its changes aren't saved over another instance's.
/// </summary>
internal sealed class LeaseKeeper : IAsyncDisposable
{
  private readonly ISagaWorkQueue _queue;
  private readonly SagaLease _lease;
  private readonly NativeOrchestratorOptions _options;
  private readonly TimeProvider _timeProvider;
  private readonly ILogger _logger;
  private readonly CancellationTokenSource _run;
  private readonly CancellationTokenSource _stop = new();
  private readonly CancellationTokenSource _loopToken;
  private readonly Task _loop;

  public LeaseKeeper(
    ISagaWorkQueue queue,
    SagaLease lease,
    NativeOrchestratorOptions options,
    TimeProvider timeProvider,
    ILogger logger,
    CancellationToken stoppingToken)
  {
    _queue = queue;
    _lease = lease;
    _options = options;
    _timeProvider = timeProvider;
    _logger = logger;
    _run = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
    _loopToken = CancellationTokenSource.CreateLinkedTokenSource(_run.Token, _stop.Token);
    _loop = RenewAsync(_loopToken.Token);
  }

  /// <summary>Cancelled when the lease is lost or the host stops.</summary>
  public CancellationToken Token => _run.Token;

  /// <summary>Stops renewing. Call it before the final save.</summary>
  public async Task StopAsync()
  {
    _stop.Cancel();
    await _loop.ConfigureAwait(false);
  }

  public async ValueTask DisposeAsync()
  {
    await StopAsync().ConfigureAwait(false);

    _loopToken.Dispose();
    _stop.Dispose();
    _run.Dispose();
  }

  private async Task RenewAsync(CancellationToken token)
  {
    while (!token.IsCancellationRequested)
    {
      try
      {
        await Task.Delay(_options.LeaseRenewalInterval, _timeProvider, token).ConfigureAwait(false);

        var renewed = await _queue.RenewLeaseAsync(_lease, _timeProvider.GetUtcNow() + _options.LeaseDuration, token).ConfigureAwait(false);

        if (!renewed)
        {
          SagaLog.LeaseLost(_logger, _lease.SagaId);

          await _run.CancelAsync().ConfigureAwait(false);

          return;
        }
      }
      catch (OperationCanceledException) when (token.IsCancellationRequested)
      {
        return;
      }
      catch (Exception ex)
      {
        // A transient store error: keep trying until the lease expires. The final save checks the lease anyway.
        SagaLog.LeaseRenewalFailed(_logger, ex, _lease.SagaId);
      }
    }
  }
}
