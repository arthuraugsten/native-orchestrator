namespace NativeOrchestrator;

/// <summary>Why a saga stopped running its steps and started compensating.</summary>
public enum SagaFailureKind
{
  /// <summary>A step returned <see cref="StepResult.Failure(string)"/>, or ran out of retries.</summary>
  StepFailed,

  /// <summary>A step ran longer than its <see cref="StepOptions.Timeout"/>.</summary>
  StepTimedOut,

  /// <summary>The saga ran longer than its <see cref="SagaOptions.MaxDuration"/>.</summary>
  SagaTimedOut,

  /// <summary>Cancellation was requested through <see cref="ISagaClient.CancelAsync"/>.</summary>
  Cancelled,
}
