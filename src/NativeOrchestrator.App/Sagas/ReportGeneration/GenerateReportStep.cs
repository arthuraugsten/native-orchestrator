using System.Net.Http.Json;
using NativeOrchestrator.App.ExternalServices;
using NativeOrchestrator.App.Simulated;

namespace NativeOrchestrator.App.Sagas.ReportGeneration;

/// <summary>Starts the report, polls until it is built, and keeps its download link. Compensation discards the report.</summary>
public sealed class GenerateReportStep : HttpPollingSagaStep<ReportGenerationData>
{
  private readonly IHttpClientFactory _httpClientFactory;

  public GenerateReportStep(IHttpClientFactory httpClientFactory)
    : base(httpClientFactory)
  {
    _httpClientFactory = httpClientFactory;
  }

  protected override string ClientName => ExternalServiceNames.Reports;

  protected override HttpRequestMessage CreateStartRequest(StepContext<ReportGenerationData> context) =>
    new(HttpMethod.Post, "reports")
    {
      Content = JsonContent.Create(new ReportRequest(context.Data.ReportName, context.Data.DurationSeconds), AppJsonContext.Default.ReportRequest),
    };

  protected override async Task<PollResult> ReadStatusAsync(
    StepContext<ReportGenerationData> context,
    HttpResponseMessage response,
    CancellationToken cancellationToken)
  {
    var report = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.ReportResponse, cancellationToken)
      ?? throw new InvalidOperationException("The report API returned no body.");

    if (report.Status == "failed")
      return PollResult.Failed($"The report failed: {report.Error}");

    context.Data.DownloadUrl = report.DownloadUrl;

    return PollResult.Succeeded();
  }

  public override async Task CompensateAsync(StepContext<ReportGenerationData> context, CancellationToken cancellationToken)
  {
    // The operation saved by the polling step is the report's status URL, which is also its resource URL.
    if (!context.TryGetState<HttpPollingOperation>(out var operation))
      return;

    using var request = new HttpRequestMessage(HttpMethod.Delete, operation.StatusUri);
    using var response = await _httpClientFactory.CreateClient(ClientName).SendAsync(request, cancellationToken);

    SagaHttp.ThrowIfFailed(response);
  }
}
