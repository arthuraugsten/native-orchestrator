using System.Net.Http.Json;
using NativeOrchestrator.App.ExternalServices;
using NativeOrchestrator.App.Simulated;

namespace NativeOrchestrator.App.Sagas.CustomerOnboarding;

/// <summary>
/// Starts an identity check, then polls it until it is approved or rejected. The check can take days; between polls,
/// the saga is stored and nothing waits in memory.
/// </summary>
public sealed class VerifyIdentityStep : HttpPollingSagaStep<CustomerOnboardingData>
{
  public VerifyIdentityStep(IHttpClientFactory httpClientFactory)
    : base(httpClientFactory)
  {
  }

  protected override string ClientName => ExternalServiceNames.Identity;

  protected override HttpRequestMessage CreateStartRequest(StepContext<CustomerOnboardingData> context) =>
    new(HttpMethod.Post, "verifications")
    {
      Content = JsonContent.Create(new VerificationRequest(context.Data.CustomerName), AppJsonContext.Default.VerificationRequest),
    };

  protected override async Task<PollResult> ReadStatusAsync(
    StepContext<CustomerOnboardingData> context,
    HttpResponseMessage response,
    CancellationToken cancellationToken)
  {
    var verification = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.VerificationResponse, cancellationToken)
      ?? throw new InvalidOperationException("The identity API returned no body.");
    context.Data.VerificationId = verification.VerificationId;
    return verification.Status switch
    {
      "approved" => PollResult.Succeeded(),
      "rejected" => PollResult.Failed($"Identity verification was rejected: {verification.Reason}"),
      _ => PollResult.Pending(),
    };
  }
}
