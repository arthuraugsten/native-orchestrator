namespace NativeOrchestrator;

/// <summary>What happened when a saga was started.</summary>
public enum SagaStartOutcome
{
  /// <summary>A new saga was created.</summary>
  Created,

  /// <summary>A saga of the same type with the same correlation identifier already existed; it was returned.</summary>
  Duplicate,

  /// <summary>
  /// The saga type is <see cref="SagaOptions.SingleInstance"/> and one is still active, or failed and awaiting manual
  /// action (see <see cref="SagaOptions.FailedBlocksNewStarts"/>); it was returned, and the new data was not used.
  /// </summary>
  AlreadyRunning,
}
