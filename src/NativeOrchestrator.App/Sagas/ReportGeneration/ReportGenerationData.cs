namespace NativeOrchestrator.App.Sagas.ReportGeneration;

/// <summary>The report saga's data.</summary>
public sealed class ReportGenerationData
{
  public string ReportName { get; set; } = "";

  public List<string> Recipients { get; set; } = [];

  /// <summary>How long the simulated warehouse takes to build the report. Real reports take hours.</summary>
  public int DurationSeconds { get; set; } = 20;

  /// <summary>Set by <see cref="GenerateReportStep"/>.</summary>
  public string? DownloadUrl { get; set; }
}
