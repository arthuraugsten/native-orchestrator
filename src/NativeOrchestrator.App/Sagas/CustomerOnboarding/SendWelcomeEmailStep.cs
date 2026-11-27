using System.Net.Http.Json;
using NativeOrchestrator.App.ExternalServices;
using NativeOrchestrator.App.Simulated;

namespace NativeOrchestrator.App.Sagas.CustomerOnboarding;

/// <summary>Sends the welcome email. The saga declares it with <c>ContinueOnError</c>: a bounce doesn't undo the onboarding.</summary>
public sealed class SendWelcomeEmailStep : HttpSagaStep<CustomerOnboardingData>
{
  public SendWelcomeEmailStep(IHttpClientFactory httpClientFactory)
    : base(httpClientFactory)
  {
  }

  protected override string ClientName => ExternalServiceNames.Notifications;

  public override async Task<StepResult> ExecuteAsync(StepContext<CustomerOnboardingData> context, CancellationToken cancellationToken)
  {
    var data = context.Data;
    var email = new EmailRequest(data.Email, "Welcome!", $"Hi {data.CustomerName}, your account {data.AccountId} is ready.");
    using var request = new HttpRequestMessage(HttpMethod.Post, "email")
    {
      Content = JsonContent.Create(email, AppJsonContext.Default.EmailRequest),
    };
    using var response = await SendAsync(request, context, cancellationToken);
    return StepResult.Success();
  }
}
