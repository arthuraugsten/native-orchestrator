namespace NativeOrchestrator;

/// <summary>Wakes the worker early when this instance starts or cancels a saga, instead of waiting for the next scan.</summary>
internal sealed class SagaWorkSignal
{
  private TaskCompletionSource _signal = NewSignal();

  public void Notify() => Volatile.Read(ref _signal).TrySetResult();

  /// <summary>Waits until <see cref="Notify"/> is called or <paramref name="timeout"/> passes.</summary>
  public async Task WaitAsync(TimeSpan timeout, TimeProvider timeProvider, CancellationToken cancellationToken)
  {
    var signal = Volatile.Read(ref _signal);
    using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    var delay = Task.Delay(timeout, timeProvider, delayCancellation.Token);
    await Task.WhenAny(delay, signal.Task).ConfigureAwait(false);
    await delayCancellation.CancelAsync().ConfigureAwait(false);

    if (signal.Task.IsCompleted)
      Interlocked.CompareExchange(ref _signal, NewSignal(), signal);
    cancellationToken.ThrowIfCancellationRequested();
  }

  private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
