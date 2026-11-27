namespace NativeOrchestrator;

/// <summary>Names to subscribe to for traces and metrics, for example with OpenTelemetry.</summary>
public static class SagaOrchestratorDiagnostics
{
  /// <summary>The <see cref="System.Diagnostics.ActivitySource"/> name. Each run of a saga is one activity.</summary>
  public const string ActivitySourceName = "SagaOrchestrator";

  /// <summary>
  /// The <see cref="System.Diagnostics.Metrics.Meter"/> name. Instruments: <c>sagaorchestrator.steps</c> (count, by
  /// saga type, step and outcome), <c>sagaorchestrator.step.duration</c> (milliseconds) and
  /// <c>sagaorchestrator.sagas.finished</c> (count, by saga type and status).
  /// </summary>
  public const string MeterName = "SagaOrchestrator";
}
