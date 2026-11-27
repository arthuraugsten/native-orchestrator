namespace NativeOrchestrator;

/// <summary>Where a saga is in its lifecycle.</summary>
public enum SagaStatus
{
  /// <summary>Created; no step has run yet.</summary>
  Pending,

  /// <summary>Running its steps, and due to run again now.</summary>
  Running,

  /// <summary>Running its steps, and waiting for a poll or a retry that is due later.</summary>
  Waiting,

  /// <summary>Undoing its succeeded steps, in reverse order, after a failure, a timeout or a cancellation.</summary>
  Compensating,

  /// <summary>Finished: every step succeeded (or failed with <see cref="StepOptions.ContinueOnError"/>).</summary>
  Completed,

  /// <summary>Finished: the saga failed, and every succeeded step was compensated.</summary>
  Compensated,

  /// <summary>Finished: a compensation failed after all its retries. Needs manual action.</summary>
  Failed,
}
