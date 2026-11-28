namespace NativeOrchestrator;

/// <summary>What <see cref="ISagaClient.CancelAsync"/> returns.</summary>
public enum CancellationRequestResult
{
  /// <summary>Cancellation was requested. The saga compensates its succeeded steps the next time it runs.</summary>
  Requested,

  /// <summary>Cancellation had already been requested.</summary>
  AlreadyRequested,

  /// <summary>The saga has finished or is already compensating, so it can't be cancelled.</summary>
  NotCancellable,

  /// <summary>No saga has that identifier.</summary>
  NotFound,
}
