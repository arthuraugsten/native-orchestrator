using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NativeOrchestrator;

/// <summary>The library's activity source and metric instruments.</summary>
internal sealed class SagaDiagnostics : IDisposable
{
  private static readonly ActivitySource Source = new(NativeOrchestratorDiagnostics.ActivitySourceName);

  private readonly Meter _meter;
  private readonly Counter<long> _steps;
  private readonly Histogram<double> _stepDuration;
  private readonly Counter<long> _finished;
  private readonly Counter<long> _occurrences;

  public SagaDiagnostics(IMeterFactory? meterFactory = null)
  {
    _meter = meterFactory?.Create(NativeOrchestratorDiagnostics.MeterName) ?? new Meter(NativeOrchestratorDiagnostics.MeterName);
    _steps = _meter.CreateCounter<long>("nativeorchestrator.steps", description: "Step runs, by outcome.");
    _stepDuration = _meter.CreateHistogram<double>("nativeorchestrator.step.duration", "ms", "How long one step run took.");
    _finished = _meter.CreateCounter<long>("nativeorchestrator.sagas.finished", description: "Sagas that finished, by status.");
    _occurrences = _meter.CreateCounter<long>("nativeorchestrator.schedule.occurrences", description: "Schedule occurrences, by outcome.");
  }

  public static Activity? StartRun(SagaRecord saga)
  {
    var activity = Source.StartActivity("saga.run");
    activity?.SetTag("saga.id", saga.Id);
    activity?.SetTag("saga.type", saga.SagaType);
    activity?.SetTag("saga.status", saga.Status.ToString());
    return activity;
  }

  public void RecordStep(string sagaType, string stepName, string outcome, TimeSpan duration)
  {
    var tags = new TagList
    {
      { "saga.type", sagaType },
      { "step.name", stepName },
      { "outcome", outcome },
    };
    _steps.Add(1, tags);
    _stepDuration.Record(duration.TotalMilliseconds, tags);
  }

  public void RecordFinished(string sagaType, SagaStatus status) =>
    _finished.Add(1, new TagList { { "saga.type", sagaType }, { "status", status.ToString() } });

  public void RecordOccurrences(string scheduleName, ScheduleOutcome outcome, int count = 1) =>
    _occurrences.Add(count, new TagList { { "schedule.name", scheduleName }, { "outcome", outcome.ToString() } });

  public void Dispose() => _meter.Dispose();
}
