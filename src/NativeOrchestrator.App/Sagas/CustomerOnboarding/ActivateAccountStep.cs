using NativeOrchestrator.App.ExternalServices;

namespace NativeOrchestrator.App.Sagas.CustomerOnboarding;

/// <summary>Activates the account once the identity is verified.</summary>
public sealed class ActivateAccountStep : HttpSagaStep<CustomerOnboardingData>
{
  public ActivateAccountStep(IHttpClientFactory httpClientFactory)
    : base(httpClientFactory) { }

  protected override string ClientName => ExternalServiceNames.Accounts;

  public override async Task<StepResult> ExecuteAsync(StepContext<CustomerOnboardingData> context, CancellationToken cancellationToken)
  {
    var accountId = context.Data.AccountId ?? throw new StepFailedException("No account was reserved.");

    using var request = new HttpRequestMessage(HttpMethod.Post, $"accounts/{Uri.EscapeDataString(accountId)}/activate");
    using var response = await SendAsync(request, context, cancellationToken);

    return StepResult.Success();
  }
}
