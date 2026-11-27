using System.Net.Http.Json;
using NativeOrchestrator.App.ExternalServices;
using NativeOrchestrator.App.Simulated;

namespace NativeOrchestrator.App.Sagas.CustomerOnboarding;

/// <summary>Creates the customer's account, still inactive. Compensation deletes it.</summary>
public sealed class ReserveAccountStep : HttpSagaStep<CustomerOnboardingData>
{
  public ReserveAccountStep(IHttpClientFactory httpClientFactory)
    : base(httpClientFactory)
  {
  }

  protected override string ClientName => ExternalServiceNames.Accounts;

  public override async Task<StepResult> ExecuteAsync(StepContext<CustomerOnboardingData> context, CancellationToken cancellationToken)
  {
    var data = context.Data;

    using var request = new HttpRequestMessage(HttpMethod.Post, "accounts")
    {
      Content = JsonContent.Create(new CreateAccountRequest(data.CustomerName, data.Email), AppJsonContext.Default.CreateAccountRequest),
    };

    using var response = await SendAsync(request, context, cancellationToken);

    var account = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.AccountResponse, cancellationToken)
      ?? throw new InvalidOperationException("The accounts API returned no body.");

    data.AccountId = account.AccountId;

    return StepResult.Success();
  }

  public override async Task CompensateAsync(StepContext<CustomerOnboardingData> context, CancellationToken cancellationToken)
  {
    if (context.Data.AccountId is not { } accountId)
      return;

    using var request = new HttpRequestMessage(HttpMethod.Delete, $"accounts/{Uri.EscapeDataString(accountId)}");
    using var response = await SendAsync(request, context, cancellationToken);
  }
}
