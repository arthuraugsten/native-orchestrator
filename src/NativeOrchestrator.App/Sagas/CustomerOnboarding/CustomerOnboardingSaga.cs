namespace NativeOrchestrator.App.Sagas.CustomerOnboarding;

/// <summary>
/// Onboards a customer: reserves an account, waits for the identity check (polled; it can take days), activates the
/// account and sends a welcome email. If the identity check is rejected or times out, the account is deleted.
/// </summary>
public sealed class CustomerOnboardingSaga : Saga<CustomerOnboardingData>
{
  public const string Name = "customer-onboarding";

  protected override void Configure(ISagaBuilder<CustomerOnboardingData> saga) => saga
    .Step<ReserveAccountStep>()
    .Step<VerifyIdentityStep>(configure: step => step.Timeout = TimeSpan.FromDays(2))
    .Step<ActivateAccountStep>()
    .Step<SendWelcomeEmailStep>(configure: step => step.ContinueOnError = true);

  protected override string? Validate(CustomerOnboardingData data)
  {
    if (string.IsNullOrWhiteSpace(data.CustomerName))
      return "customerName is required.";

    if (string.IsNullOrWhiteSpace(data.Email) || !data.Email.Contains('@', StringComparison.Ordinal))
      return "email must be an email address.";

    return null;
  }
}
