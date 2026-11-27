namespace NativeOrchestrator.Tests;

public sealed class SagaRecordTests
{
  private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

  [Fact]
  public void Restore_rebuilds_a_saga_and_its_steps_from_stored_values()
  {
    var id = Guid.CreateVersion7();
    var steps = new[]
    {
      StepRecord.Restore(1, "B", StepStatus.InProgress, 2, 3, 0, "\"op\"", Now, null, "timeout"),
      StepRecord.Restore(0, "A", StepStatus.Succeeded, 0, 0, 0, null, Now, Now, null),
    };

    var saga = SagaRecord.Restore(
      id,
      "orders",
      "order-42",
      "orders",
      SagaStatus.Waiting,
      currentStepIndex: 1,
      dataJson: """{"total":10}""",
      createdAtUtc: Now,
      updatedAtUtc: Now.AddMinutes(5),
      startedAtUtc: Now.AddMinutes(1),
      completedAtUtc: null,
      nextRunAtUtc: Now.AddHours(1),
      cancellationRequested: false,
      failureKind: null,
      failureStep: null,
      error: null,
      version: 7,
      steps: steps);

    Assert.Equal(id, saga.Id);
    Assert.Equal("orders", saga.SagaType);
    Assert.Equal("order-42", saga.CorrelationId);
    Assert.Equal("orders", saga.ActiveInstanceKey);
    Assert.Equal(SagaStatus.Waiting, saga.Status);
    Assert.Equal(1, saga.CurrentStepIndex);
    Assert.Equal("""{"total":10}""", saga.DataJson);
    Assert.Equal(Now.AddMinutes(5), saga.UpdatedAtUtc);
    Assert.Equal(Now.AddHours(1), saga.NextRunAtUtc);
    Assert.Equal(7, saga.Version);

    // Steps come back in declaration order, whatever order the store read them in.
    Assert.Equal(["A", "B"], saga.Steps.Select(s => s.Name));
    var polling = saga.Steps[1];
    Assert.Equal(2, polling.Attempt);
    Assert.Equal(3, polling.CheckCount);
    Assert.Equal("\"op\"", polling.StateJson);
    Assert.Equal("timeout", polling.Error);
  }

  [Fact]
  public void Requesting_cancellation_makes_the_saga_due_now_and_moves_its_version_on_once()
  {
    var saga = SagaRecord.Create(Guid.CreateVersion7(), "orders", null, null, "{}", Now);

    Assert.Equal(CancellationRequestResult.Requested, saga.RequestCancellation(Now.AddMinutes(1)));
    Assert.True(saga.CancellationRequested);
    Assert.Equal(Now.AddMinutes(1), saga.NextRunAtUtc);
    Assert.Equal(Now.AddMinutes(1), saga.UpdatedAtUtc);
    Assert.Equal(1, saga.Version);

    Assert.Equal(CancellationRequestResult.AlreadyRequested, saga.RequestCancellation(Now.AddMinutes(2)));
    Assert.Equal(1, saga.Version);
  }

  [Fact]
  public void A_finished_saga_cannot_be_cancelled()
  {
    var saga = SagaRecord.Create(Guid.CreateVersion7(), "orders", null, null, "{}", Now);
    saga.Finish(SagaStatus.Completed, Now, failedBlocksNewStarts: true);

    Assert.Equal(CancellationRequestResult.NotCancellable, saga.RequestCancellation(Now));
    Assert.False(saga.CancellationRequested);
    Assert.Equal(0, saga.Version);
  }

  [Theory]
  [InlineData(SagaStatus.Completed, true, null)]
  [InlineData(SagaStatus.Compensated, true, null)]
  [InlineData(SagaStatus.Failed, true, "orders")]
  [InlineData(SagaStatus.Failed, false, null)]
  public void Finishing_frees_the_single_instance_key_unless_a_failed_saga_blocks_new_starts(
    SagaStatus status,
    bool failedBlocksNewStarts,
    string? expectedKey)
  {
    var saga = SagaRecord.Create(Guid.CreateVersion7(), "orders", null, "orders", "{}", Now);

    saga.Finish(status, Now.AddHours(1), failedBlocksNewStarts);

    Assert.Equal(status, saga.Status);
    Assert.Equal(Now.AddHours(1), saga.CompletedAtUtc);
    Assert.Null(saga.NextRunAtUtc);
    Assert.Equal(expectedKey, saga.ActiveInstanceKey);
  }

  [Fact]
  public void Starting_compensation_fails_the_step_in_progress_and_keeps_why_the_saga_failed()
  {
    var saga = SagaRecord.Create(Guid.CreateVersion7(), "orders", null, null, "{}", Now);
    saga.Start(Now);
    saga.StartStep(0, "A", Now).Succeed(Now);
    saga.MoveToNextStep();
    saga.StartStep(1, "B", Now);

    saga.BeginCompensation(SagaFailureKind.Cancelled, null, "Cancellation was requested.", Now.AddMinutes(1));

    Assert.Equal(SagaStatus.Compensating, saga.Status);
    Assert.Equal(SagaFailureKind.Cancelled, saga.FailureKind);
    Assert.Equal("Cancellation was requested.", saga.Error);
    Assert.Equal(Now.AddMinutes(1), saga.NextRunAtUtc);
    Assert.Equal([StepStatus.Succeeded, StepStatus.Failed], saga.Steps.Select(s => s.Status));

    // A compensating saga that waits for a retry stays compensating.
    saga.WaitUntil(Now.AddMinutes(10));
    Assert.Equal(SagaStatus.Compensating, saga.Status);
  }

  [Fact]
  public void A_step_cannot_start_twice()
  {
    var saga = SagaRecord.Create(Guid.CreateVersion7(), "orders", null, null, "{}", Now);
    saga.StartStep(0, "A", Now);

    Assert.Throws<InvalidOperationException>(() => saga.StartStep(0, "A", Now));
  }
}
