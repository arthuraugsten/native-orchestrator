namespace NativeOrchestrator;

/// <summary>
/// Receives each history entry after it has been saved: saga started, step succeeded, compensation started, and so on.
/// Use it to publish events, send alerts or update read models. Register it with
/// <see cref="NativeOrchestratorBuilder.AddObserver{TObserver}"/>.
/// </summary>
/// <remarks>
/// Observers are notified at most once, after the save; an exception is logged and doesn't affect the saga. For
/// guaranteed delivery, read the history from the store instead.
/// </remarks>
public interface ISagaLifecycleObserver
{
  /// <summary>Handles one history entry.</summary>
  /// <param name="entry">The entry.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  ValueTask OnEventAsync(SagaHistoryEntry entry, CancellationToken cancellationToken);
}
