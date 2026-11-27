using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NativeOrchestrator;

/// <summary>
/// The hosted service that starts sagas on their schedules. Every instance runs it: each occurrence is started with a
/// correlation ID made from the schedule and the occurrence time, so the store's correlation index lets exactly one
/// instance create the saga. Nothing is stored for the schedule itself; after a restart, the latest occurrence within
/// <see cref="SagaScheduleOptions.MisfireThreshold"/> is started if no instance started it yet.
/// </summary>
internal sealed class SagaScheduler : BackgroundService
{
  // Wakes at least this often, so configuration changes (a new CRON expression, Enabled) apply soon.
  private static readonly TimeSpan MaxSleep = TimeSpan.FromMinutes(1);

  private readonly SagaScheduleRegistry _registry;
  private readonly ISagaClient _client;
  private readonly IOptionsMonitor<SagaScheduleOptions> _scheduleOptions;
  private readonly SagaOrchestratorOptions _options;
  private readonly TimeProvider _timeProvider;
  private readonly SagaDiagnostics _diagnostics;
  private readonly ILogger<SagaScheduler> _logger;

  // Per schedule: occurrences up to this time have been handled by this instance.
  private readonly Dictionary<string, DateTimeOffset> _handledUntil = new(StringComparer.Ordinal);
  private readonly Dictionary<string, CronExpression> _parsed = new(StringComparer.Ordinal);

  public SagaScheduler(
    SagaScheduleRegistry registry,
    ISagaClient client,
    IOptionsMonitor<SagaScheduleOptions> scheduleOptions,
    IOptions<SagaOrchestratorOptions> options,
    TimeProvider timeProvider,
    SagaDiagnostics diagnostics,
    ILogger<SagaScheduler> logger)
  {
    _registry = registry;
    _client = client;
    _scheduleOptions = scheduleOptions;
    _options = options.Value;
    _timeProvider = timeProvider;
    _diagnostics = diagnostics;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (_registry.Schedules.Count == 0)
      return;
    if (!_options.WorkerEnabled)
    {
      SagaLog.SchedulerDisabled(_logger);
      return;
    }

    SagaLog.SchedulerStarted(_logger, _registry.Schedules.Count);
    try
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        var wakeAt = await RunDueAsync(stoppingToken).ConfigureAwait(false);
        var delay = wakeAt - _timeProvider.GetUtcNow();
        if (delay > TimeSpan.Zero)
          await Task.Delay(delay, _timeProvider, stoppingToken).ConfigureAwait(false);
      }
    }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
    {
      // Shutting down.
    }
  }

  /// <summary>Starts every occurrence that is due now and returns when to check again.</summary>
  internal async Task<DateTimeOffset> RunDueAsync(CancellationToken cancellationToken)
  {
    var now = _timeProvider.GetUtcNow();
    var wakeAt = now + MaxSleep;
    foreach (var schedule in _registry.Schedules)
    {
      var options = _scheduleOptions.Get(schedule.Name);
      if (!options.Enabled)
      {
        // A paused schedule doesn't catch up when it is turned back on.
        _handledUntil[schedule.Name] = now;
        continue;
      }

      var zone = SagaSchedule.FindTimeZone(options.TimeZone) ?? TimeZoneInfo.Utc;
      var cron = Parse(schedule.Name, options.Cron!);
      var retry = await StartDueAsync(schedule, options, cron, zone, now, cancellationToken).ConfigureAwait(false);
      if (retry && now + _options.ScanInterval < wakeAt)
        wakeAt = now + _options.ScanInterval;
      if (cron.GetNextOccurrence(now, zone) is { } next && next < wakeAt)
        wakeAt = next;
    }

    return wakeAt;
  }

  /// <summary>Starts the latest due occurrence of a schedule. Returns whether it failed and should be tried again soon.</summary>
  private async Task<bool> StartDueAsync(
    SagaSchedule schedule,
    SagaScheduleOptions options,
    CronExpression cron,
    TimeZoneInfo zone,
    DateTimeOffset now,
    CancellationToken cancellationToken)
  {
    var earliest = now - options.MisfireThreshold;
    // On the first check (startup), earlier occurrences are unknown territory (another instance may have started
    // them), so only the latest one within the threshold is considered, and none are counted as missed.
    var known = _handledUntil.TryGetValue(schedule.Name, out var handled);
    var after = known ? handled : earliest - TimeSpan.FromTicks(1);
    DateTimeOffset? latest = null;
    var misfired = 0;
    for (var occurrence = cron.GetNextOccurrence(after, zone); occurrence is { } at && at <= now; occurrence = cron.GetNextOccurrence(at, zone))
    {
      if (latest is not null && known)
        misfired++;
      latest = at;
    }

    if (latest < earliest)
    {
      misfired++;
      latest = null;
    }

    if (misfired > 0)
    {
      SagaLog.OccurrencesMisfired(_logger, schedule.Name, misfired, options.MisfireThreshold);
      _diagnostics.RecordOccurrences(schedule.Name, ScheduleOutcome.Misfired, misfired);
    }

    if (latest is not { } scheduledAt)
    {
      _handledUntil[schedule.Name] = now;
      return false;
    }

    object data;
    try
    {
      data = schedule.CreateData(new ScheduledOccurrence(schedule.Name, scheduledAt));
    }
    catch (Exception ex)
    {
      // A bug in the factory fails the same way on every try, so it isn't retried.
      Rejected(schedule, scheduledAt, ex, now);
      return false;
    }

    SagaStartResult result;
    try
    {
      result = await _client.StartAsync(schedule.SagaType, data, schedule.CorrelationId(scheduledAt), cancellationToken).ConfigureAwait(false);
    }
    catch (ArgumentException ex)
    {
      // The saga's Validate rejected the data: also the same on every try.
      Rejected(schedule, scheduledAt, ex, now);
      return false;
    }
    catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
    {
      // Most likely the store is unavailable. The occurrence stays due and is tried again while within the threshold;
      // earlier ones are already counted, so the next check starts just before it.
      SagaLog.OccurrenceFailed(_logger, ex, schedule.Name, scheduledAt, _options.ScanInterval);
      _diagnostics.RecordOccurrences(schedule.Name, ScheduleOutcome.Failed);
      _handledUntil[schedule.Name] = scheduledAt - TimeSpan.FromTicks(1);
      return true;
    }

    switch (result.Outcome)
    {
      case SagaStartOutcome.Created:
        SagaLog.OccurrenceStarted(_logger, schedule.Name, result.SagaId, scheduledAt);
        _diagnostics.RecordOccurrences(schedule.Name, ScheduleOutcome.Started);
        break;
      case SagaStartOutcome.Duplicate:
        SagaLog.OccurrenceDuplicate(_logger, schedule.Name, scheduledAt, result.SagaId);
        _diagnostics.RecordOccurrences(schedule.Name, ScheduleOutcome.Duplicate);
        break;
      default:
        SagaLog.OccurrenceSkipped(_logger, schedule.Name, scheduledAt, result.SagaId);
        _diagnostics.RecordOccurrences(schedule.Name, ScheduleOutcome.Skipped);
        break;
    }

    _handledUntil[schedule.Name] = now;
    return false;
  }

  private void Rejected(SagaSchedule schedule, DateTimeOffset scheduledAt, Exception exception, DateTimeOffset now)
  {
    SagaLog.OccurrenceRejected(_logger, exception, schedule.Name, scheduledAt);
    _diagnostics.RecordOccurrences(schedule.Name, ScheduleOutcome.Failed);
    _handledUntil[schedule.Name] = now;
  }

  private CronExpression Parse(string scheduleName, string text)
  {
    if (_parsed.TryGetValue(scheduleName, out var cron) && cron.ToString() == text.Trim())
      return cron;
    // Validated at startup and on configuration reload, so this doesn't throw.
    cron = CronExpression.Parse(text);
    _parsed[scheduleName] = cron;
    return cron;
  }
}
