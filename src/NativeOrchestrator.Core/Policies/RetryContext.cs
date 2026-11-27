namespace NativeOrchestrator;

/// <summary>A transient failure, for <see cref="IRetryPolicy"/> to decide on.</summary>
/// <param name="SagaType">The saga type name.</param>
/// <param name="StepName">The step's name.</param>
/// <param name="FailureCount">How many times in a row the step has now failed (1 after the first failure).</param>
/// <param name="Exception">What the step threw.</param>
/// <param name="Options">The step's effective retry options.</param>
/// <param name="IsCompensation">Whether the failure is in the step's compensation.</param>
public readonly record struct RetryContext(
  string SagaType,
  string StepName,
  int FailureCount,
  Exception Exception,
  RetryOptions Options,
  bool IsCompensation);
