namespace NativeOrchestrator;

/// <summary>A step's options once overrides are applied: configuration, then code, then the saga's options.</summary>
internal sealed record EffectiveStepOptions(
  TimeSpan Timeout,
  TimeSpan ExecutionTimeout,
  bool ContinueOnError,
  RetryOptions Retry,
  PollingOptions Polling,
  RetryOptions CompensationRetry)
{
  public static EffectiveStepOptions Resolve(SagaOptions saga, StepDefinition step)
  {
    saga.Steps.TryGetValue(step.Name, out var configured);

    var code = step.Options;

    return new EffectiveStepOptions(
      configured?.Timeout ?? code.Timeout ?? saga.StepTimeout,
      configured?.ExecutionTimeout ?? code.ExecutionTimeout ?? saga.ExecutionTimeout,
      configured?.ContinueOnError ?? code.ContinueOnError ?? false,
      configured?.Retry ?? code.Retry ?? saga.Retry,
      configured?.Polling ?? code.Polling ?? saga.Polling,
      configured?.CompensationRetry ?? code.CompensationRetry ?? saga.CompensationRetry);
  }
}
