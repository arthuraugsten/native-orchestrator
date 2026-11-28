using Microsoft.Extensions.DependencyInjection;

namespace NativeOrchestrator.Tests;

public sealed class StepExecutionTests
{
  [Fact]
  public async Task Steps_run_in_declaration_order_and_the_saga_completes()
  {
    await using var host = SagaTestHost.Create(new SagaShape().Step("A").Step("B").Step("C"));
    var id = await host.StartAsync();

    var saga = await host.RunUntilFinishedAsync(id);

    Assert.Equal(SagaStatus.Completed, saga.Status);
    Assert.Equal(["A@1", "B@1", "C@1"], host.Script.Journal);
    Assert.Equal(["A", "B", "C"], saga.Data.GetProperty("trace").EnumerateArray().Select(e => e.GetString()));
    Assert.All(saga.Steps, step => Assert.Equal(StepStatus.Succeeded, step.Status));
    Assert.Null(saga.NextRunAtUtc);
    Assert.Null(saga.CurrentStep);
    Assert.Equal(1, host.Runs);
    Assert.Equal(["completed:Completed"], host.Script.Finished);

    var history = await host.Client.GetHistoryAsync(id);
    Assert.Equal(
      [
        SagaEventType.Created, SagaEventType.Started,
        SagaEventType.StepStarted, SagaEventType.StepSucceeded,
        SagaEventType.StepStarted, SagaEventType.StepSucceeded,
        SagaEventType.StepStarted, SagaEventType.StepSucceeded,
        SagaEventType.Completed,
      ],
      history.Select(h => h.EventType));
  }

  [Fact]
  public async Task Transient_failures_are_retried_with_backoff_and_data_from_failed_attempts_is_dropped()
  {
    await using var host = SagaTestHost.Create(new SagaShape().Step("A").Step("B"));
    host.Script.Execute["B"] = context => context.Attempt < 3 ? throw new HttpRequestException("Service unavailable") : StepResult.Success();
    var id = await host.StartAsync();

    var saga = await host.RunUntilFinishedAsync(id);

    Assert.Equal(SagaStatus.Completed, saga.Status);
    Assert.Equal(["A@1", "B@1", "B@2", "B@3"], host.Script.Journal);
    // Delays of 10 s, then 20 s.
    Assert.Equal(
      [0, 0, 10, 30],
      host.Script.Timeline.Select(t => (t.At - SagaTestHost.Start).TotalSeconds));
    // Each failed attempt added "B" to the trace, but only the successful one was saved.
    Assert.Equal(["A", "B"], saga.Data.GetProperty("trace").EnumerateArray().Select(e => e.GetString()));
    Assert.Equal(0, saga.Steps[1].Attempt);

    var retries = (await host.Client.GetHistoryAsync(id)).Where(h => h.EventType == SagaEventType.StepRetryScheduled).ToArray();
    Assert.Equal(2, retries.Length);
    Assert.Contains("Service unavailable", retries[0].Details);
  }

  [Fact]
  public async Task A_step_that_runs_out_of_retries_fails_the_saga_and_compensates_the_earlier_steps()
  {
    await using var host = SagaTestHost.Create(new SagaShape().Step("A").Step("B").Step("C"));
    host.Script.Execute["C"] = _ => throw new InvalidOperationException("still broken");
    var id = await host.StartAsync();

    var saga = await host.RunUntilFinishedAsync(id);

    Assert.Equal(SagaStatus.Compensated, saga.Status);
    Assert.Equal(["A@1", "B@1", "C@1", "C@2", "C@3", "undo:B@1", "undo:A@1"], host.Script.Journal);
    Assert.Equal(SagaFailureKind.StepFailed, saga.Failure?.Kind);
    Assert.Equal("C", saga.Failure?.StepName);
    Assert.Contains("after 3 attempts: still broken", saga.Failure?.Error);
    Assert.Equal([StepStatus.Compensated, StepStatus.Compensated, StepStatus.Failed], saga.Steps.Select(s => s.Status));
    Assert.Equal(["failed:Compensated:StepFailed"], host.Script.Finished);
  }

  [Fact]
  public async Task A_permanent_failure_is_not_retried()
  {
    await using var host = SagaTestHost.Create(new SagaShape().Step("A").Step("B"));
    host.Script.Execute["B"] = _ => StepResult.Failure("card declined");
    var id = await host.StartAsync();

    var saga = await host.RunUntilFinishedAsync(id);

    Assert.Equal(SagaStatus.Compensated, saga.Status);
    Assert.Equal(["A@1", "B@1", "undo:A@1"], host.Script.Journal);
    Assert.Equal("card declined", saga.Failure?.Error);
  }

  [Fact]
  public async Task Throwing_StepFailedException_is_a_permanent_failure()
  {
    await using var host = SagaTestHost.Create(new SagaShape().Step("A"));
    host.Script.Execute["A"] = _ => throw new StepFailedException("rejected");
    var id = await host.StartAsync();

    var saga = await host.RunUntilFinishedAsync(id);

    Assert.Equal(SagaStatus.Compensated, saga.Status);
    Assert.Equal(["A@1"], host.Script.Journal);
    Assert.Equal("rejected", saga.Failure?.Error);
  }

  [Fact]
  public async Task A_failing_step_with_ContinueOnError_does_not_stop_the_saga()
  {
    await using var host = SagaTestHost.Create(new SagaShape().Step("A").Step("Notify", o => o.ContinueOnError = true).Step("C"));
    host.Script.Execute["Notify"] = _ => StepResult.Failure("mailbox full");
    var id = await host.StartAsync();

    var saga = await host.RunUntilFinishedAsync(id);

    Assert.Equal(SagaStatus.Completed, saga.Status);
    Assert.Null(saga.Failure);
    Assert.Equal([StepStatus.Succeeded, StepStatus.Failed, StepStatus.Succeeded], saga.Steps.Select(s => s.Status));
    Assert.Equal("mailbox full", saga.Steps[1].Error);
  }

  [Fact]
  public async Task A_failed_compensation_is_retried_then_the_saga_fails_and_the_other_compensations_still_run()
  {
    await using var host = SagaTestHost.Create(new SagaShape().Step("A").Step("B").Step("C"));
    host.Script.Execute["C"] = _ => StepResult.Failure("boom");
    host.Script.Compensate["B"] = _ => throw new TimeoutException("refund service down");
    var id = await host.StartAsync();

    var saga = await host.RunUntilFinishedAsync(id);

    Assert.Equal(SagaStatus.Failed, saga.Status);
    Assert.Equal(["A@1", "B@1", "C@1", "undo:B@1", "undo:B@2", "undo:B@3", "undo:A@1"], host.Script.Journal);
    Assert.Equal([StepStatus.Compensated, StepStatus.CompensationFailed, StepStatus.Failed], saga.Steps.Select(s => s.Status));
    Assert.Contains("refund service down", saga.Steps[1].Error);
    Assert.Equal(["failed:Failed:StepFailed"], host.Script.Finished);
  }

  [Fact]
  public async Task Configuration_overrides_step_options_set_in_code()
  {
    await using var host = SagaTestHost.Create(
      new SagaShape().Step("A", o => o.Retry = new RetryOptions { MaxAttempts = 5, InitialDelay = TimeSpan.FromSeconds(1) }),
      configuration: new Dictionary<string, string?> { ["Sagas:scripted:Steps:A:Retry:MaxAttempts"] = "2" });
    host.Script.Execute["A"] = _ => throw new InvalidOperationException("nope");
    var id = await host.StartAsync();

    var saga = await host.RunUntilFinishedAsync(id);

    Assert.Equal(["A@1", "A@2"], host.Script.Journal);
    Assert.Contains("after 2 attempts", saga.Failure?.Error);
  }

  [Fact]
  public async Task Saga_defaults_from_configuration_apply_unless_the_saga_overrides_them()
  {
    await using var host = SagaTestHost.Create(
      new SagaShape().Step("A"),
      configuration: new Dictionary<string, string?>
      {
        ["SagaDefaults:MaxDuration"] = "1.00:00:00",
        ["Sagas:scripted:StepTimeout"] = "02:00:00",
      });

    var options = host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<SagaOptions>>().Get(SagaTestHost.SagaType);

    Assert.Equal(TimeSpan.FromDays(1), options.MaxDuration);
    Assert.Equal(TimeSpan.FromHours(2), options.StepTimeout);
    Assert.Equal(3, options.Retry.MaxAttempts);
  }
}
