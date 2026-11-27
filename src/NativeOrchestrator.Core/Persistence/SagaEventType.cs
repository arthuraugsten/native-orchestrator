namespace NativeOrchestrator;

/// <summary>What a <see cref="SagaHistoryEntry"/> records.</summary>
public enum SagaEventType
{
  /// <summary>The saga was created.</summary>
  Created,

  /// <summary>The saga's first step started.</summary>
  Started,

  /// <summary>A step started.</summary>
  StepStarted,

  /// <summary>A step is waiting for its operation to finish (recorded on the first poll only).</summary>
  StepWaiting,

  /// <summary>A step failed transiently and will be retried.</summary>
  StepRetryScheduled,

  /// <summary>A step succeeded.</summary>
  StepSucceeded,

  /// <summary>A step failed.</summary>
  StepFailed,

  /// <summary>A step ran longer than its timeout.</summary>
  StepTimedOut,

  /// <summary>The saga started compensating its succeeded steps.</summary>
  CompensationStarted,

  /// <summary>A step's compensation failed transiently and will be retried.</summary>
  CompensationRetryScheduled,

  /// <summary>A step was compensated.</summary>
  StepCompensated,

  /// <summary>A step's compensation failed after all its retries.</summary>
  StepCompensationFailed,

  /// <summary>The saga completed.</summary>
  Completed,

  /// <summary>The saga failed and every succeeded step was compensated.</summary>
  Compensated,

  /// <summary>The saga failed and at least one compensation failed.</summary>
  Failed,
}
