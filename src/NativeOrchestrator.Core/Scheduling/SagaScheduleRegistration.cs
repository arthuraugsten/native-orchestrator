namespace NativeOrchestrator;

/// <summary>A schedule added with <c>AddSchedule</c>. <see cref="SagaScheduleRegistry"/> checks it against the sagas at startup.</summary>
internal sealed record SagaScheduleRegistration(string Name, string SagaType, Type DataType, Func<ScheduledOccurrence, object> CreateData);
