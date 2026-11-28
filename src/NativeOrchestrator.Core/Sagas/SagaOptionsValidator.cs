using Microsoft.Extensions.Options;

namespace NativeOrchestrator;

internal sealed class SagaOptionsValidator : IValidateOptions<SagaOptions>
{
  public ValidateOptionsResult Validate(string? name, SagaOptions options)
  {
    var errors = new List<string>();
    var prefix = $"Sagas:{name}";
    if (options.MaxDuration <= TimeSpan.Zero)
      errors.Add($"{prefix}:MaxDuration must be positive.");
    if (options.StepTimeout <= TimeSpan.Zero)
      errors.Add($"{prefix}:StepTimeout must be positive.");
    if (options.ExecutionTimeout <= TimeSpan.Zero)
      errors.Add($"{prefix}:ExecutionTimeout must be positive.");
    ValidateRetry(errors, $"{prefix}:Retry", options.Retry);
    ValidateBackoff(errors, $"{prefix}:Polling", options.Polling);
    ValidateRetry(errors, $"{prefix}:CompensationRetry", options.CompensationRetry);

    foreach (var (stepName, step) in options.Steps)
      ValidateStep(errors, $"{prefix}:Steps:{stepName}", step);

    return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
  }

  internal static void ValidateStep(List<string> errors, string prefix, StepOptions step)
  {
    if (step.Timeout <= TimeSpan.Zero)
      errors.Add($"{prefix}:Timeout must be positive.");
    if (step.ExecutionTimeout <= TimeSpan.Zero)
      errors.Add($"{prefix}:ExecutionTimeout must be positive.");
    if (step.Retry is not null)
      ValidateRetry(errors, $"{prefix}:Retry", step.Retry);
    if (step.Polling is not null)
      ValidateBackoff(errors, $"{prefix}:Polling", step.Polling);
    if (step.CompensationRetry is not null)
      ValidateRetry(errors, $"{prefix}:CompensationRetry", step.CompensationRetry);
  }

  internal static void ValidateRetry(List<string> errors, string prefix, RetryOptions? retry)
  {
    if (retry is null)
    {
      errors.Add($"{prefix} must be set.");
      return;
    }

    if (retry.MaxAttempts < 1)
      errors.Add($"{prefix}:MaxAttempts must be at least 1.");
    ValidateBackoff(errors, prefix, retry);
  }

  internal static void ValidateBackoff(List<string> errors, string prefix, BackoffOptions? backoff)
  {
    if (backoff is null)
    {
      errors.Add($"{prefix} must be set.");
      return;
    }

    if (backoff.InitialDelay < TimeSpan.Zero)
      errors.Add($"{prefix}:InitialDelay must not be negative.");
    if (backoff.MaxDelay < backoff.InitialDelay)
      errors.Add($"{prefix}:MaxDelay must not be shorter than InitialDelay.");
    if (backoff.Multiplier < 1 || double.IsNaN(backoff.Multiplier) || double.IsInfinity(backoff.Multiplier))
      errors.Add($"{prefix}:Multiplier must be a number of at least 1.");
  }
}
