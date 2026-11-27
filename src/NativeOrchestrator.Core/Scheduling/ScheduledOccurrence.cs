namespace NativeOrchestrator;

/// <summary>One occurrence of a schedule, passed to the schedule's data factory.</summary>
/// <param name="ScheduleName">The schedule's name.</param>
/// <param name="ScheduledAtUtc">When the occurrence was due. It can be earlier than now if it starts late.</param>
public sealed record ScheduledOccurrence(string ScheduleName, DateTimeOffset ScheduledAtUtc);
