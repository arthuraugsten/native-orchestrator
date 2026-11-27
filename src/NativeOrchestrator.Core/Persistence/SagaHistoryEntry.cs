namespace NativeOrchestrator;

/// <summary>One entry of a saga's append-only history. Also what <see cref="ISagaLifecycleObserver"/>s receive.</summary>
/// <param name="SagaId">The saga's identifier.</param>
/// <param name="SagaType">The saga type name.</param>
/// <param name="TimestampUtc">When it happened.</param>
/// <param name="EventType">What happened.</param>
/// <param name="StepName">The step it happened to, if any.</param>
/// <param name="Details">A description, if any.</param>
public sealed record SagaHistoryEntry(
  Guid SagaId,
  string SagaType,
  DateTimeOffset TimestampUtc,
  SagaEventType EventType,
  string? StepName,
  string? Details);
