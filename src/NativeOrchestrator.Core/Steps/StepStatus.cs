namespace NativeOrchestrator;

/// <summary>Where a step is in its lifecycle.</summary>
public enum StepStatus
{
  /// <summary>Started (and, for a polling step, waiting for the operation to finish), or waiting for a retry.</summary>
  InProgress,

  /// <summary>Finished successfully.</summary>
  Succeeded,

  /// <summary>Failed. If the step has <see cref="StepOptions.ContinueOnError"/>, the saga carried on.</summary>
  Failed,

  /// <summary>Succeeded, then was compensated.</summary>
  Compensated,

  /// <summary>Succeeded, then its compensation failed after all its retries.</summary>
  CompensationFailed,
}
