namespace NativeOrchestrator.App.Observability;

/// <summary>
/// An example lifecycle observer: logs each saga event. Replace it with one that publishes events to a message bus or
/// alerts when a saga ends in <see cref="SagaEventType.Failed"/> (which needs manual action).
/// </summary>
public sealed partial class LoggingSagaObserver : ISagaLifecycleObserver
{
  private readonly ILogger<LoggingSagaObserver> _logger;

  public LoggingSagaObserver(ILogger<LoggingSagaObserver> logger)
  {
    _logger = logger;
  }

  public ValueTask OnEventAsync(SagaHistoryEntry entry, CancellationToken cancellationToken)
  {
    if (entry.EventType == SagaEventType.Failed)
      LogNeedsAttention(entry.SagaId, entry.SagaType);
    else
      LogEvent(entry.SagaType, entry.SagaId, entry.EventType, entry.StepName, entry.Details);
    return ValueTask.CompletedTask;
  }

  [LoggerMessage(LogLevel.Information, "Saga {SagaType} {SagaId}: {EventType} {StepName} {Details}")]
  private partial void LogEvent(string sagaType, Guid sagaId, SagaEventType eventType, string? stepName, string? details);

  [LoggerMessage(LogLevel.Critical, "Saga {SagaType} {SagaId} failed and couldn't compensate every step; it needs manual action")]
  private partial void LogNeedsAttention(Guid sagaId, string sagaType);
}
