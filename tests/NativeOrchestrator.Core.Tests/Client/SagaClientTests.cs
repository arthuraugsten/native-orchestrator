using System.Text.Json;

namespace NativeOrchestrator.Tests;

public sealed class SagaClientTests
{
  [Fact]
  public async Task Starting_twice_with_the_same_correlation_id_returns_the_existing_saga()
  {
    await using var host = SagaTestHost.Create(new SagaShape().Step("A"));

    var first = await host.Client.StartAsync(SagaTestHost.SagaType, new TestData(), "order-42");
    var second = await host.Client.StartAsync(SagaTestHost.SagaType, new TestData(), "order-42");

    Assert.True(first.Created);
    Assert.False(second.Created);
    Assert.Equal(SagaStartOutcome.Duplicate, second.Outcome);
    Assert.Equal(first.SagaId, second.SagaId);
  }

  [Fact]
  public async Task A_single_instance_saga_cannot_start_while_one_is_active_and_can_once_it_finishes()
  {
    await using var host = SagaTestHost.Create(new SagaShape().Step("A").PollingStep("B"), options => options.SingleInstance = true);

    var first = await host.Client.StartAsync(SagaTestHost.SagaType, new TestData());
    await host.RunUntilWaitingAsync(first.SagaId);
    var whileWaiting = await host.Client.StartAsync(SagaTestHost.SagaType, new TestData(), "another-caller");

    Assert.Equal(SagaStartOutcome.Created, first.Outcome);
    Assert.Equal(SagaStartOutcome.AlreadyRunning, whileWaiting.Outcome);
    Assert.False(whileWaiting.Created);
    Assert.Equal(first.SagaId, whileWaiting.SagaId);

    host.Script.Check["B"] = _ => PollResult.Succeeded();
    var finished = await host.RunUntilFinishedAsync(first.SagaId);
    Assert.Equal(SagaStatus.Completed, finished.Status);

    var next = await host.Client.StartAsync(SagaTestHost.SagaType, new TestData());
    Assert.Equal(SagaStartOutcome.Created, next.Outcome);
    Assert.NotEqual(first.SagaId, next.SagaId);
  }

  [Fact]
  public async Task A_single_instance_saga_is_released_when_it_compensates()
  {
    await using var host = SagaTestHost.Create(new SagaShape().Step("A").Step("B"), options => options.SingleInstance = true);
    host.Script.Execute["B"] = _ => StepResult.Failure("No.");

    var first = await host.Client.StartAsync(SagaTestHost.SagaType, new TestData());
    Assert.Equal(SagaStatus.Compensated, (await host.RunUntilFinishedAsync(first.SagaId)).Status);

    Assert.Equal(SagaStartOutcome.Created, (await host.Client.StartAsync(SagaTestHost.SagaType, new TestData())).Outcome);
  }

  [Fact]
  public async Task A_failed_single_instance_saga_keeps_blocking_new_starts()
  {
    await using var host = SagaTestHost.Create(new SagaShape().Step("A").Step("B"), options => options.SingleInstance = true);
    host.Script.Execute["B"] = _ => StepResult.Failure("No.");
    host.Script.Compensate["A"] = _ => throw new TimeoutException("undo service down");

    var first = await host.Client.StartAsync(SagaTestHost.SagaType, new TestData());
    Assert.Equal(SagaStatus.Failed, (await host.RunUntilFinishedAsync(first.SagaId)).Status);

    var next = await host.Client.StartAsync(SagaTestHost.SagaType, new TestData());
    Assert.Equal(SagaStartOutcome.AlreadyRunning, next.Outcome);
    Assert.Equal(first.SagaId, next.SagaId);
  }

  [Theory]
  [InlineData("code")]
  [InlineData("configuration")]
  public async Task A_failed_single_instance_saga_releases_new_starts_when_failed_blocking_is_turned_off(string where)
  {
    await using var host = SagaTestHost.Create(
      new SagaShape().Step("A").Step("B"),
      options =>
      {
        options.SingleInstance = true;
        options.FailedBlocksNewStarts = where != "code";
      },
      configuration: where == "configuration"
        ? new Dictionary<string, string?> { [$"Sagas:{SagaTestHost.SagaType}:FailedBlocksNewStarts"] = "false" }
        : null);
    host.Script.Execute["B"] = _ => StepResult.Failure("No.");
    host.Script.Compensate["A"] = _ => throw new TimeoutException("undo service down");

    var first = await host.Client.StartAsync(SagaTestHost.SagaType, new TestData());
    Assert.Equal(SagaStatus.Failed, (await host.RunUntilFinishedAsync(first.SagaId)).Status);

    var next = await host.Client.StartAsync(SagaTestHost.SagaType, new TestData());
    Assert.Equal(SagaStartOutcome.Created, next.Outcome);
    Assert.NotEqual(first.SagaId, next.SagaId);
  }

  [Fact]
  public async Task Single_instance_can_be_turned_on_in_configuration_and_is_off_by_default()
  {
    await using var configured = SagaTestHost.Create(
      new SagaShape().Step("A"),
      configuration: new Dictionary<string, string?> { [$"Sagas:{SagaTestHost.SagaType}:SingleInstance"] = "true" });
    await using var unconfigured = SagaTestHost.Create(new SagaShape().Step("A"));

    await configured.Client.StartAsync(SagaTestHost.SagaType, new TestData());
    await unconfigured.Client.StartAsync(SagaTestHost.SagaType, new TestData());

    Assert.Equal(SagaStartOutcome.AlreadyRunning, (await configured.Client.StartAsync(SagaTestHost.SagaType, new TestData())).Outcome);
    Assert.Equal(SagaStartOutcome.Created, (await unconfigured.Client.StartAsync(SagaTestHost.SagaType, new TestData())).Outcome);
  }

  [Fact]
  public async Task Starting_with_json_data_validates_it_against_the_saga_data_type()
  {
    await using var host = SagaTestHost.Create(new SagaShape().Step("A"));
    using var valid = JsonDocument.Parse("""{"trace":["given"]}""");
    using var invalid = JsonDocument.Parse("""{"trace":42}""");
    using var rejected = JsonDocument.Parse("""{"reject":"Customer is blocked."}""");

    var started = await host.Client.StartAsync(SagaTestHost.SagaType, valid.RootElement);
    var saga = await host.Client.GetAsync(started.SagaId);

    Assert.Equal(SagaStatus.Pending, saga?.Status);
    Assert.Equal("A", saga?.CurrentStep);
    Assert.Equal("given", saga?.Data.GetProperty("trace")[0].GetString());
    await Assert.ThrowsAsync<ArgumentException>(() => host.Client.StartAsync(SagaTestHost.SagaType, invalid.RootElement));
    var error = await Assert.ThrowsAsync<ArgumentException>(() => host.Client.StartAsync(SagaTestHost.SagaType, rejected.RootElement));
    Assert.StartsWith("Customer is blocked.", error.Message, StringComparison.Ordinal);
  }

  [Fact]
  public async Task Starting_an_unknown_saga_type_throws()
  {
    await using var host = SagaTestHost.Create(new SagaShape().Step("A"));

    var error = await Assert.ThrowsAsync<UnknownSagaTypeException>(() => host.Client.StartAsync("missing", new TestData()));
    Assert.Equal("missing", error.SagaType);
  }

  [Fact]
  public async Task Cancelling_a_waiting_saga_compensates_its_succeeded_steps()
  {
    await using var host = SagaTestHost.Create(new SagaShape().Step("A").PollingStep("B"));
    var id = await host.StartAsync();
    var waiting = await host.RunUntilWaitingAsync(id);
    Assert.Equal(SagaStatus.Waiting, waiting.Status);
    Assert.Equal("B", waiting.CurrentStep);

    Assert.Equal(CancellationRequestResult.Requested, await host.Client.CancelAsync(id));
    // Due now, not when the next check was due.
    await host.RunDueAsync();
    var saga = await host.Client.GetAsync(id);

    Assert.Equal(SagaStatus.Compensated, saga?.Status);
    Assert.Equal(SagaFailureKind.Cancelled, saga?.Failure?.Kind);
    Assert.True(saga?.CancellationRequested);
    Assert.Equal([StepStatus.Compensated, StepStatus.Failed], saga?.Steps.Select(s => s.Status));
    Assert.Equal(["A@1", "start:B", "undo:A@1"], host.Script.Journal);
    Assert.Equal(CancellationRequestResult.NotCancellable, await host.Client.CancelAsync(id));
    Assert.Equal(CancellationRequestResult.NotFound, await host.Client.CancelAsync(Guid.NewGuid()));
  }

  [Fact]
  public async Task A_cancellation_while_a_step_runs_drops_the_run_and_is_handled_on_the_next_run()
  {
    await using var host = SagaTestHost.Create(new SagaShape().Step("A").Step("B"));
    var id = await host.StartAsync();
    host.Script.Execute["B"] = context =>
    {
      // Another caller cancels while B runs: the run's save is rejected, so B's success isn't stored.
      host.Client.CancelAsync(context.SagaId).GetAwaiter().GetResult();
      return StepResult.Success();
    };

    await host.RunDueAsync();
    var afterConflict = await host.Client.GetAsync(id);
    Assert.Equal(StepStatus.Succeeded, afterConflict?.Steps[0].Status);
    Assert.Single(afterConflict!.Steps);

    await host.RunDueAsync();
    var saga = await host.Client.GetAsync(id);
    Assert.Equal(SagaStatus.Compensated, saga?.Status);
    Assert.Equal(SagaFailureKind.Cancelled, saga?.Failure?.Kind);
    Assert.Equal(["A@1", "B@1", "undo:A@1"], host.Script.Journal);
  }
}
