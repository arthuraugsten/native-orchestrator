namespace NativeOrchestrator.Tests;

public sealed class PollingTests
{
  [Fact]
  public async Task A_polling_step_starts_once_then_checks_with_growing_intervals_until_the_operation_finishes()
  {
    await using var host = SagaTestHost.Create(new SagaShape().PollingStep("Export", o => o.Polling = new PollingOptions
    {
      InitialDelay = TimeSpan.FromMinutes(1),
      Multiplier = 2,
      MaxDelay = TimeSpan.FromMinutes(10),
    }));
    host.Script.Check["Export"] = context => context.CheckCount >= 5 ? PollResult.Succeeded() : PollResult.Pending();
    var id = await host.StartAsync();

    var saga = await host.RunUntilFinishedAsync(id);

    Assert.Equal(SagaStatus.Completed, saga.Status);
    Assert.Single(host.Script.Journal, e => e == "start:Export");
    // Delays of 1, 2, 4 and 8 minutes, then 10 (capped): checks 1, 3, 7, 15 and 25 minutes after the start.
    Assert.Equal(
      [0, 1, 3, 7, 15, 25],
      host.Script.Timeline.Select(t => (t.At - SagaTestHost.Start).TotalMinutes));
    Assert.Equal(5, saga.Steps[0].CheckCount);
    Assert.Equal(["Export"], saga.Data.GetProperty("trace").EnumerateArray().Select(e => e.GetString()));
    Assert.Single(await host.Client.GetHistoryAsync(id), h => h.EventType == SagaEventType.StepWaiting);
  }

  [Fact]
  public async Task A_saga_can_poll_for_more_than_12_hours_without_holding_anything_in_memory()
  {
    await using var host = SagaTestHost.Create(
      new SagaShape()
        .Step("Request")
        .PollingStep("Export", o =>
        {
          o.Timeout = TimeSpan.FromHours(48);
          o.Polling = new PollingOptions { InitialDelay = TimeSpan.FromMinutes(15), Multiplier = 2, MaxDelay = TimeSpan.FromHours(1) };
        })
        .Step("Publish"),
      configureSaga: o => o.MaxDuration = TimeSpan.FromDays(3));
    host.Script.Check["Export"] = context =>
      context.TimeProvider.GetUtcNow() - context.StepStartedAtUtc >= TimeSpan.FromHours(26) ? PollResult.Succeeded() : PollResult.Pending();
    var id = await host.StartAsync();

    var saga = await host.RunUntilFinishedAsync(id);

    Assert.Equal(SagaStatus.Completed, saga.Status);
    Assert.True(saga.CompletedAtUtc - saga.StartedAtUtc >= TimeSpan.FromHours(26));
    // Every check is its own run: the saga is saved and released while it waits.
    Assert.Equal(saga.Steps[1].CheckCount + 1, host.Runs);
    Assert.True(host.Runs > 26);
    Assert.Equal(["Request", "Export", "Publish"], saga.Data.GetProperty("trace").EnumerateArray().Select(e => e.GetString()));
  }

  [Fact]
  public async Task Retry_after_from_the_operation_is_honoured_but_capped_at_the_maximum_delay()
  {
    await using var host = SagaTestHost.Create(new SagaShape().PollingStep("Job", o => o.Polling = new PollingOptions
    {
      InitialDelay = TimeSpan.FromMinutes(1),
      Multiplier = 1,
      MaxDelay = TimeSpan.FromMinutes(30),
    }));
    host.Script.Check["Job"] = context => context.CheckCount switch
    {
      1 => PollResult.Pending(TimeSpan.FromMinutes(5)),
      2 => PollResult.Pending(TimeSpan.FromHours(2)),
      _ => PollResult.Succeeded(),
    };
    var id = await host.StartAsync();

    await host.RunUntilFinishedAsync(id);

    Assert.Equal([0, 1, 6, 36], host.Script.Timeline.Select(t => (t.At - SagaTestHost.Start).TotalMinutes));
  }

  [Fact]
  public async Task A_check_that_throws_is_retried_without_starting_the_operation_again()
  {
    await using var host = SagaTestHost.Create(new SagaShape().PollingStep("Job"));
    host.Script.Check["Job"] = context => context.Attempt == 1 && context.CheckCount == 1
      ? throw new HttpRequestException("502")
      : context.CheckCount >= 2 ? PollResult.Succeeded() : PollResult.Pending();
    var id = await host.StartAsync();

    var saga = await host.RunUntilFinishedAsync(id);

    Assert.Equal(SagaStatus.Completed, saga.Status);
    Assert.Equal(["start:Job", "check:Job", "check:Job", "check:Job"], host.Script.Journal);
  }

  [Fact]
  public async Task A_polling_step_that_times_out_fails_and_the_earlier_steps_are_compensated_in_reverse_order()
  {
    await using var host = SagaTestHost.Create(new SagaShape()
      .Step("Reserve")
      .Step("Charge")
      .PollingStep("Ship", o => o.Timeout = TimeSpan.FromHours(2)));
    var id = await host.StartAsync();

    var saga = await host.RunUntilFinishedAsync(id);

    Assert.Equal(SagaStatus.Compensated, saga.Status);
    Assert.Equal(SagaFailureKind.StepTimedOut, saga.Failure?.Kind);
    Assert.Equal("Ship", saga.Failure?.StepName);
    Assert.Equal(["undo:Charge@1", "undo:Reserve@1"], host.Script.Journal.Where(e => e.StartsWith("undo:", StringComparison.Ordinal)));
    // The last check is due exactly at the timeout, which then fails the step.
    Assert.Equal(TimeSpan.FromHours(2), saga.Steps[2].CompletedAtUtc - saga.Steps[2].StartedAtUtc);
    Assert.Contains(await host.Client.GetHistoryAsync(id), h => h.EventType == SagaEventType.StepTimedOut);
  }

  [Fact]
  public async Task A_saga_that_exceeds_its_maximum_duration_times_out_and_compensates()
  {
    await using var host = SagaTestHost.Create(
      new SagaShape().Step("A").PollingStep("Wait", o => o.Timeout = TimeSpan.FromHours(10)),
      configureSaga: o => o.MaxDuration = TimeSpan.FromHours(1));
    var id = await host.StartAsync();

    var saga = await host.RunUntilFinishedAsync(id);

    Assert.Equal(SagaStatus.Compensated, saga.Status);
    Assert.Equal(SagaFailureKind.SagaTimedOut, saga.Failure?.Kind);
    Assert.Equal(StepStatus.Failed, saga.Steps[1].Status);
    Assert.Contains("undo:A@1", host.Script.Journal);
  }
}
