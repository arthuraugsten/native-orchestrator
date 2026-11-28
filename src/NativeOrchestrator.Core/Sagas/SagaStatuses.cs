namespace NativeOrchestrator;

/// <summary>Groups of <see cref="SagaStatus"/> values, shared by the engine and the stores.</summary>
public static class SagaStatuses
{
  /// <summary>Statuses of sagas that still have work to do, so a worker may claim them.</summary>
  public static IReadOnlyList<SagaStatus> Active { get; } =
    [SagaStatus.Pending, SagaStatus.Running, SagaStatus.Waiting, SagaStatus.Compensating];

  /// <summary>Statuses of sagas that can still be cancelled.</summary>
  public static IReadOnlyList<SagaStatus> Cancellable { get; } =
    [SagaStatus.Pending, SagaStatus.Running, SagaStatus.Waiting];

  /// <summary>Whether a saga with this status has finished.</summary>
  /// <param name="status">The status.</param>
  public static bool IsFinished(this SagaStatus status) =>
    status is SagaStatus.Completed or SagaStatus.Compensated or SagaStatus.Failed;
}
