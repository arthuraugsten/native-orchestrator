using Microsoft.Extensions.Options;

namespace NativeOrchestrator;

internal sealed class SagaOrchestratorOptionsValidator : IValidateOptions<SagaOrchestratorOptions>
{
  public ValidateOptionsResult Validate(string? name, SagaOrchestratorOptions options)
  {
    var errors = new List<string>();
    if (string.IsNullOrWhiteSpace(options.InstanceName))
      errors.Add("SagaOrchestrator:InstanceName must not be empty.");
    if (options.MaxConcurrency < 1)
      errors.Add("SagaOrchestrator:MaxConcurrency must be at least 1.");
    if (options.ScanInterval <= TimeSpan.Zero)
      errors.Add("SagaOrchestrator:ScanInterval must be positive.");
    if (options.LeaseDuration <= TimeSpan.Zero)
      errors.Add("SagaOrchestrator:LeaseDuration must be positive.");
    if (options.LeaseRenewalInterval <= TimeSpan.Zero || options.LeaseRenewalInterval >= options.LeaseDuration)
      errors.Add("SagaOrchestrator:LeaseRenewalInterval must be positive and shorter than LeaseDuration.");
    if (options.UnknownSagaTypeRetryDelay <= TimeSpan.Zero)
      errors.Add("SagaOrchestrator:UnknownSagaTypeRetryDelay must be positive.");
    if (options.MaxErrorLength < 100)
      errors.Add("SagaOrchestrator:MaxErrorLength must be at least 100.");
    return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
  }
}
