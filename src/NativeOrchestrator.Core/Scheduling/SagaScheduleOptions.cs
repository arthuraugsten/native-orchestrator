namespace NativeOrchestrator;

/// <summary>
/// Options for one schedule, as named options (the name is the schedule name). Set in code by
/// <see cref="SagaOrchestratorBuilder.AddSchedule{TData}"/>, then overridden by <c>Schedules:{name}</c> when the
/// orchestrator is added with a configuration section. Read at every occurrence, so configuration changes apply without
/// a restart.
/// </summary>
public sealed class SagaScheduleOptions
{
  /// <summary>
  /// When to start the saga: a CRON expression with five fields (<c>minute hour day-of-month month day-of-week</c>), or
  /// six with seconds first, or a macro such as <c>@daily</c>.
  /// </summary>
  public string? Cron { get; set; }

  /// <summary>
  /// The time zone the expression is read in: a system time-zone identifier (IANA, such as <c>Europe/Lisbon</c>, or
  /// Windows). Defaults to <c>UTC</c>.
  /// </summary>
  public string TimeZone { get; set; } = "UTC";

  /// <summary>Whether the schedule starts sagas. Turn it off to pause it without a deployment. Defaults to <see langword="true"/>.</summary>
  public bool Enabled { get; set; } = true;

  /// <summary>
  /// How late an occurrence may still start, for example after the app was down when it was due. Only the latest
  /// missed occurrence starts, never a backlog. <see cref="TimeSpan.Zero"/> never starts a missed occurrence. Defaults
  /// to 1 hour.
  /// </summary>
  public TimeSpan MisfireThreshold { get; set; } = TimeSpan.FromHours(1);
}
