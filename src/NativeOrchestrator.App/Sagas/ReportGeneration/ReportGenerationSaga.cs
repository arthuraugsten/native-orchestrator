namespace NativeOrchestrator.App.Sagas.ReportGeneration;

/// <summary>
/// Builds a report in the data warehouse (polled; it can take many hours), then emails the link to the recipients.
/// If the email can't be sent, the generated report is discarded.
/// </summary>
public sealed class ReportGenerationSaga : Saga<ReportGenerationData>
{
  public const string Name = "report-generation";

  protected override void Configure(ISagaBuilder<ReportGenerationData> saga) => saga
    .Step<GenerateReportStep>(configure: step => step.Timeout = TimeSpan.FromHours(48))
    .Step<PublishReportStep>();

  protected override string? Validate(ReportGenerationData data)
  {
    if (string.IsNullOrWhiteSpace(data.ReportName))
      return "reportName is required.";

    if (data.Recipients.Count == 0)
      return "recipients must list at least one email address.";

    if (data.DurationSeconds is < 0 or > 7 * 24 * 3600)
      return "durationSeconds must be between 0 and a week.";

    return null;
  }
}
