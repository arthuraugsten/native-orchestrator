namespace NativeOrchestrator;

/// <summary>A registered schedule, checked against its saga type.</summary>
internal sealed class SagaSchedule
{
  private readonly Func<ScheduledOccurrence, object> _createData;

  public SagaSchedule(SagaScheduleRegistration registration)
  {
    Name = registration.Name;
    SagaType = registration.SagaType;
    _createData = registration.CreateData;
  }

  public string Name { get; }

  public string SagaType { get; }

  /// <summary>The correlation ID that makes each occurrence start at most one saga, whichever instance starts it.</summary>
  public string CorrelationId(DateTimeOffset scheduledAtUtc) =>
    $"schedule:{Name}:{scheduledAtUtc.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}";

  public object CreateData(ScheduledOccurrence occurrence) =>
    _createData(occurrence) ?? throw new InvalidOperationException($"The data factory of schedule '{Name}' returned null.");

  /// <summary>Finds a time zone by IANA or Windows identifier, or returns <see langword="null"/>.</summary>
  public static TimeZoneInfo? FindTimeZone(string? id)
  {
    if (string.IsNullOrWhiteSpace(id))
      return null;
    if (string.Equals(id, "UTC", StringComparison.OrdinalIgnoreCase))
      return TimeZoneInfo.Utc;
    return TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : null;
  }
}
