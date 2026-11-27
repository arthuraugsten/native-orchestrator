namespace NativeOrchestrator;

/// <summary>Names to subscribe to for traces and metrics, for example with OpenTelemetry.</summary>
public static class NativeOrchestratorDiagnostics
{
  /// <summary>The <see cref="System.Diagnostics.ActivitySource"/> name. Each run of a saga is one activity.</summary>
  public const string ActivitySourceName = "NativeOrchestrator";

  /// <summary>
  /// The <see cref="System.Diagnostics.Metrics.Meter"/> name. Instruments: <c>nativeorchestrator.steps</c> (count, by
  /// saga type, step and outcome), <c>nativeorchestrator.step.duration</c> (milliseconds) and
  /// <c>nativeorchestrator.sagas.finished</c> (count, by saga type and status).
  /// </summary>
  public const string MeterName = "NativeOrchestrator";
}
