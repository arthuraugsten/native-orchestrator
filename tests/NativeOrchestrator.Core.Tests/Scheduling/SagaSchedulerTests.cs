using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NativeOrchestrator.Tests;

public sealed class SagaSchedulerTests
{
  private const string Schedule = "hourly";

  [Fact]
  public async Task Each_occurrence_starts_one_saga_when_it_is_due()
  {
    await using var host = Create(new SagaShape().Step("A"), schedule: o => o.MisfireThreshold = TimeSpan.Zero);
    var started = Track(host);
    host.Time.SetUtcNow(SagaTestHost.Start.AddMinutes(10));

    // It wakes at least once a minute, to see configuration changes.
    Assert.Equal(SagaTestHost.Start.AddMinutes(11), await host.RunSchedulesAsync());
    Assert.Equal(0, await host.RunDueAsync());

    host.Time.SetUtcNow(SagaTestHost.Start.AddHours(1));
    await host.RunSchedulesAsync();
    await host.RunSchedulesAsync();
    await host.RunDueAsync();

    // Close to the next occurrence, it wakes exactly at it.
    host.Time.SetUtcNow(SagaTestHost.Start.AddHours(2).AddSeconds(-30));
    Assert.Equal(SagaTestHost.Start.AddHours(2), await host.RunSchedulesAsync());
    host.Time.SetUtcNow(SagaTestHost.Start.AddHours(2));
    await host.RunSchedulesAsync();
    await host.RunDueAsync();

    Assert.Equal(["01:00", "02:00"], started);
  }

  [Fact]
  public async Task An_occurrence_is_started_once_by_instances_that_share_a_store()
  {
    var store = new InMemorySagaStore();
    await using var first = Create(new SagaShape().Step("A"), store: store);
    await using var second = Create(new SagaShape().Step("A"), store: store);
    first.Time.SetUtcNow(SagaTestHost.Start.AddHours(1));
    second.Time.SetUtcNow(SagaTestHost.Start.AddHours(1));

    await first.RunSchedulesAsync();
    await second.RunSchedulesAsync();

    Assert.Equal(1, await first.RunDueAsync());
    Assert.Equal(0, await second.RunDueAsync());
    var existing = await first.Client.StartAsync(SagaTestHost.SagaType, new TestData(), $"schedule:{Schedule}:20260101T010000Z", TestContext.Current.CancellationToken);
    Assert.Equal(SagaStartOutcome.Duplicate, existing.Outcome);
  }

  [Fact]
  public async Task After_downtime_only_the_latest_missed_occurrence_starts_if_it_is_within_the_misfire_threshold()
  {
    await using var host = Create(new SagaShape().Step("A"));
    var started = Track(host);
    host.Time.SetUtcNow(SagaTestHost.Start.AddHours(3).AddMinutes(20));

    await host.RunSchedulesAsync();
    await host.RunDueAsync();

    Assert.Equal(["03:00"], started);
  }

  [Fact]
  public async Task A_missed_occurrence_past_the_misfire_threshold_doesnt_start()
  {
    await using var host = Create(new SagaShape().Step("A"), schedule: o => o.MisfireThreshold = TimeSpan.FromMinutes(10));
    host.Time.SetUtcNow(SagaTestHost.Start.AddHours(1).AddMinutes(20));

    await host.RunSchedulesAsync();

    Assert.Equal(0, await host.RunDueAsync());
  }

  [Fact]
  public async Task Occurrences_missed_while_the_scheduler_was_paused_start_only_the_latest()
  {
    await using var host = Create(new SagaShape().Step("A"));
    var started = Track(host);
    host.Time.SetUtcNow(SagaTestHost.Start.AddMinutes(30));
    await host.RunSchedulesAsync();
    await host.RunDueAsync();

    host.Time.SetUtcNow(SagaTestHost.Start.AddHours(3).AddMinutes(30));
    await host.RunSchedulesAsync();
    await host.RunDueAsync();

    Assert.Equal(["00:00", "03:00"], started);
  }

  [Fact]
  public async Task An_occurrence_is_skipped_while_a_single_instance_saga_is_still_active()
  {
    await using var host = Create(new SagaShape().Step("A").PollingStep("B"), saga: o => o.SingleInstance = true);
    var started = Track(host);
    host.Time.SetUtcNow(SagaTestHost.Start.AddHours(1));
    await host.RunSchedulesAsync();
    await host.RunDueAsync();

    host.Time.SetUtcNow(SagaTestHost.Start.AddHours(2));
    await host.RunSchedulesAsync();
    Assert.Equal(["01:00"], started);

    // The skipped occurrence isn't started late once the running saga finishes.
    host.Script.Check["B"] = _ => PollResult.Succeeded();
    await host.RunDueAsync();
    host.Time.SetUtcNow(SagaTestHost.Start.AddHours(2).AddMinutes(1));
    await host.RunSchedulesAsync();
    Assert.Equal(0, await host.RunDueAsync());

    host.Time.SetUtcNow(SagaTestHost.Start.AddHours(3));
    await host.RunSchedulesAsync();
    await host.RunDueAsync();
    Assert.Equal(["01:00", "03:00"], started);
  }

  [Fact]
  public async Task A_disabled_schedule_starts_nothing()
  {
    await using var host = Create(new SagaShape().Step("A"), configuration: new() { [$"Schedules:{Schedule}:Enabled"] = "false" });
    host.Time.SetUtcNow(SagaTestHost.Start.AddHours(1));

    await host.RunSchedulesAsync();

    Assert.Equal(0, await host.RunDueAsync());
  }

  [Fact]
  public async Task Configuration_overrides_the_schedule_set_in_code()
  {
    await using var host = Create(
      new SagaShape().Step("A"),
      configuration: new() { [$"Schedules:{Schedule}:Cron"] = "30 * * * *", [$"Schedules:{Schedule}:TimeZone"] = "Europe/Lisbon" });

    var started = Track(host);
    var options = host.Services.GetRequiredService<IOptionsMonitor<SagaScheduleOptions>>().Get(Schedule);
    host.Time.SetUtcNow(SagaTestHost.Start.AddMinutes(30));

    await host.RunSchedulesAsync();
    await host.RunDueAsync();

    Assert.Equal("30 * * * *", options.Cron);
    Assert.Equal("Europe/Lisbon", options.TimeZone);
    // Lisbon is UTC+0 in January.
    Assert.Equal(["00:30"], started);
  }

  [Fact]
  public async Task Data_the_saga_rejects_skips_the_occurrence_without_retrying()
  {
    await using var host = Create(new SagaShape().Step("A"), data: _ => new TestData { Reject = "No reports today." });
    host.Time.SetUtcNow(SagaTestHost.Start.AddHours(1));

    var wakeAt = await host.RunSchedulesAsync();

    Assert.Equal(0, await host.RunDueAsync());
    // Not retried after the scan interval, as a store failure would be.
    Assert.Equal(SagaTestHost.Start.AddHours(1).AddMinutes(1), wakeAt);
  }

  [Fact]
  public async Task An_occurrence_that_fails_to_start_is_tried_again_after_the_scan_interval()
  {
    var store = new InMemorySagaStore();
    var repository = new FailingRepository(store);
    await using var host = SagaTestHost.Create(
      new SagaShape().Step("A"),
      useStore: b =>
      {
        b.Services.AddSingleton(store);
        b.UseInMemoryStore();
        b.Services.AddSingleton<ISagaRepository>(repository);
      },
      configureOrchestrator: b => b.AddSchedule(Schedule, SagaTestHost.SagaType, "0 * * * *", o => new TestData { Trace = [o.ScheduledAtUtc.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)] }));
    var started = Track(host);
    host.Time.SetUtcNow(SagaTestHost.Start.AddHours(1));

    repository.Fail = true;
    Assert.Equal(SagaTestHost.Start.AddHours(1).AddSeconds(5), await host.RunSchedulesAsync());
    repository.Fail = false;
    host.Time.SetUtcNow(SagaTestHost.Start.AddHours(1).AddSeconds(5));
    await host.RunSchedulesAsync();
    await host.RunDueAsync();

    Assert.Equal(["01:00"], started);
  }

  [Theory]
  [InlineData("Cron", "61 * * * *", "Schedules:hourly:Cron is invalid")]
  [InlineData("Cron", "0 0 30 2 *", "never occurs")]
  [InlineData("TimeZone", "Mars/Olympus", "Schedules:hourly:TimeZone 'Mars/Olympus' isn't a known time zone")]
  [InlineData("MisfireThreshold", "-00:01:00", "Schedules:hourly:MisfireThreshold must not be negative")]
  public async Task Invalid_schedule_options_fail_validation(string key, string value, string message)
  {
    await using var host = Create(new SagaShape().Step("A"), configuration: new() { [$"Schedules:{Schedule}:{key}"] = value });

    var error = Assert.Throws<OptionsValidationException>(
      () => host.Services.GetRequiredService<IOptionsMonitor<SagaScheduleOptions>>().Get(Schedule));
    Assert.Contains(error.Failures, f => f.Contains(message, StringComparison.Ordinal));
  }

  [Fact]
  public async Task A_schedule_for_an_unregistered_saga_fails_when_the_registry_is_built()
  {
    await using var host = SagaTestHost.Create(
      new SagaShape().Step("A"),
      configureOrchestrator: b => b.AddSchedule("orphan", "missing", "@daily", _ => new TestData()));

    var error = Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<SagaScheduleRegistry>());
    Assert.Contains("starts saga 'missing', which isn't registered", error.Message);
  }

  [Fact]
  public async Task A_schedule_with_the_wrong_data_type_fails_when_the_registry_is_built()
  {
    await using var host = SagaTestHost.Create(
      new SagaShape().Step("A"),
      configureOrchestrator: b => b.AddSchedule("wrong", SagaTestHost.SagaType, "@daily", _ => "text"));

    var error = Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<SagaScheduleRegistry>());
    Assert.Contains("creates String data, but saga 'scripted' takes TestData", error.Message);
  }

  [Fact]
  public void Registering_two_schedules_with_the_same_name_throws()
  {
    var builder = new ServiceCollection().AddSagaOrchestrator();
    builder.AddSchedule("nightly", "orders", "@daily", _ => new TestData());

    Assert.Throws<InvalidOperationException>(() => builder.AddSchedule("nightly", "orders", "@hourly", _ => new TestData()));
  }

  /// <summary>A host with an hourly schedule whose data records the occurrence time (HH:mm) as the first trace entry.</summary>
  private static SagaTestHost Create(
    SagaShape shape,
    Action<SagaScheduleOptions>? schedule = null,
    Action<SagaOptions>? saga = null,
    Dictionary<string, string?>? configuration = null,
    InMemorySagaStore? store = null,
    Func<ScheduledOccurrence, TestData>? data = null)
  {
    return SagaTestHost.Create(
      shape,
      configureSaga: saga,
      configuration: configuration,
      useStore: store is null ? null : b =>
      {
        b.Services.AddSingleton(store);
        b.UseInMemoryStore();
      },
      configureOrchestrator: b => b.AddSchedule(
        Schedule,
        SagaTestHost.SagaType,
        "0 * * * *",
        data ?? (o => new TestData { Trace = [o.ScheduledAtUtc.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)] }),
        schedule));
  }

  /// <summary>A repository whose creates fail while <see cref="Fail"/> is set, as when the database is down.</summary>
  private sealed class FailingRepository : ISagaRepository
  {
    private readonly InMemorySagaStore _store;

    public FailingRepository(InMemorySagaStore store)
    {
      _store = store;
    }

    public bool Fail { get; set; }

    public Task CreateAsync(SagaRecord saga, SagaHistoryEntry created, CancellationToken cancellationToken) =>
      Fail ? throw new TimeoutException("The database is down.") : _store.CreateAsync(saga, created, cancellationToken);

    public Task<SagaRecord?> GetAsync(Guid sagaId, CancellationToken cancellationToken) => _store.GetAsync(sagaId, cancellationToken);

    public Task<IReadOnlyList<SagaHistoryEntry>> GetHistoryAsync(Guid sagaId, CancellationToken cancellationToken) =>
      _store.GetHistoryAsync(sagaId, cancellationToken);

    public Task<CancellationRequestResult> RequestCancellationAsync(Guid sagaId, DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
      _store.RequestCancellationAsync(sagaId, nowUtc, cancellationToken);
  }

  /// <summary>Records the occurrence time of every saga whose first step runs.</summary>
  private static List<string> Track(SagaTestHost host)
  {
    var started = new List<string>();
    host.Script.Execute["A"] = context =>
    {
      started.Add(context.Data.Trace[0]);
      return StepResult.Success();
    };
    return started;
  }
}
