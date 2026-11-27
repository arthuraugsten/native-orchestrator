using System.Net.Http.Json;
using NativeOrchestrator.App.ExternalServices;
using NativeOrchestrator.App.Simulated;

namespace NativeOrchestrator.App.Sagas.ReportGeneration;

/// <summary>Emails the report's link to every recipient.</summary>
public sealed class PublishReportStep : HttpSagaStep<ReportGenerationData>
{
  public PublishReportStep(IHttpClientFactory httpClientFactory)
    : base(httpClientFactory)
  {
  }

  protected override string ClientName => ExternalServiceNames.Notifications;

  public override async Task<StepResult> ExecuteAsync(StepContext<ReportGenerationData> context, CancellationToken cancellationToken)
  {
    var data = context.Data;
    foreach (var recipient in data.Recipients)
    {
      var email = new EmailRequest(recipient, $"Report ready: {data.ReportName}", $"Download it from {data.DownloadUrl}.");
      using var request = new HttpRequestMessage(HttpMethod.Post, "email")
      {
        Content = JsonContent.Create(email, AppJsonContext.Default.EmailRequest),
      };
      using var response = await SendAsync(request, context, cancellationToken);
    }

    return StepResult.Success();
  }
}
