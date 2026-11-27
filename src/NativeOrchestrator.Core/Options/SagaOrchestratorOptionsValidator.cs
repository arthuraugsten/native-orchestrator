using Microsoft.Extensions.Options;

namespace NativeOrchestrator;

internal sealed class NativeOrchestratorOptionsValidator : IValidateOptions<NativeOrchestratorOptions>
{
  public ValidateOptionsResult Validate(string? name, NativeOrchestratorOptions options)
  {
    var errors = new List<string>();
    if (string.IsNullOrWhiteSpace(options.InstanceName))
      errors.Add("NativeOrchestrator:InstanceName must not be empty.");
    if (options.MaxConcurrency < 1)
      errors.Add("NativeOrchestrator:MaxConcurrency must be at least 1.");
    if (options.ScanInterval <= TimeSpan.Zero)
      errors.Add("NativeOrchestrator:ScanInterval must be positive.");
    if (options.LeaseDuration <= TimeSpan.Zero)
      errors.Add("NativeOrchestrator:LeaseDuration must be positive.");
    if (options.LeaseRenewalInterval <= TimeSpan.Zero || options.LeaseRenewalInterval >= options.LeaseDuration)
      errors.Add("NativeOrchestrator:LeaseRenewalInterval must be positive and shorter than LeaseDuration.");
    if (options.UnknownSagaTypeRetryDelay <= TimeSpan.Zero)
      errors.Add("NativeOrchestrator:UnknownSagaTypeRetryDelay must be positive.");
    if (options.MaxErrorLength < 100)
      errors.Add("NativeOrchestrator:MaxErrorLength must be at least 100.");
    return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
  }
}
