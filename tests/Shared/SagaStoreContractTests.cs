namespace NativeOrchestrator.Tests;

/// <summary>
/// The contract every store must follow. Each store's test project derives from this class with its own store, so
/// the in-memory store and the database stores behave the same.
/// </summary>
public abstract class SagaStoreContractTests
{
  private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

  /// <summary>Creates an empty store.</summary>
  protected abstract Task<(ISagaRepository Repository, ISagaWorkQueue Queue)> CreateStoreAsync();

  [Fact]
  public async Task A_created_saga_is_read_back_with_its_history()
  {
    var (repository, _) = await CreateStoreAsync();
    var saga = NewSaga();
    await repository.CreateAsync(saga, Created(saga), Token);

    var stored = await repository.GetAsync(saga.Id, Token);
    Assert.NotNull(stored);
    Assert.Equal(saga.SagaType, stored.SagaType);
    Assert.Equal(SagaStatus.Pending, stored.Status);
    Assert.Equal(saga.DataJson, stored.DataJson);
    Assert.Equal(saga.NextRunAtUtc, stored.NextRunAtUtc);
    Assert.Equal(0, stored.Version);
    Assert.Empty(stored.Steps);

    var history = await repository.GetHistoryAsync(saga.Id, Token);
    Assert.Equal(SagaEventType.Created, Assert.Single(history).EventType);
    Assert.Null(await repository.GetAsync(Guid.NewGuid(), Token));
  }

  [Fact]
  public async Task Correlation_ids_are_unique_per_saga_type()
  {
    var (repository, _) = await CreateStoreAsync();
    var first = NewSaga(correlationId: "order-1");
    await repository.CreateAsync(first, Created(first), Token);

    var duplicate = NewSaga(correlationId: "order-1");
    var error = await Assert.ThrowsAsync<DuplicateSagaException>(() => repository.CreateAsync(duplicate, Created(duplicate), Token));
    Assert.Equal(first.Id, error.ExistingSagaId);

    var otherType = NewSaga(correlationId: "order-1", sagaType: "other");
    await repository.CreateAsync(otherType, Created(otherType), Token);
    var noCorrelation1 = NewSaga();
    var noCorrelation2 = NewSaga();
    await repository.CreateAsync(noCorrelation1, Created(noCorrelation1), Token);
    await repository.CreateAsync(noCorrelation2, Created(noCorrelation2), Token);
  }

  [Fact]
  public async Task Active_instance_keys_are_unique_until_the_saga_holding_one_clears_it()
  {
    var (repository, queue) = await CreateStoreAsync();
    var first = NewSaga(activeInstanceKey: "contract");
    await repository.CreateAsync(first, Created(first), Token);

    var second = NewSaga(activeInstanceKey: "contract");
    var error = await Assert.ThrowsAsync<DuplicateSagaException>(() => repository.CreateAsync(second, Created(second), Token));
    Assert.Equal(first.Id, error.ExistingSagaId);
    Assert.Equal(DuplicateSagaReason.ActiveInstance, error.Reason);
    var otherKey = NewSaga(activeInstanceKey: "other");
    var noKey = NewSaga();
    await repository.CreateAsync(otherKey, Created(otherKey), Token);
    await repository.CreateAsync(noKey, Created(noKey), Token);

    // The engine clears the key when the saga finishes, which frees it.
    var (saga, lease) = Assert.Single(await queue.ClaimDueAsync(Claim(maxCount: 10), Token), c => c.Saga.Id == first.Id);
    saga.Finish(SagaStatus.Completed, Now, failedBlocksNewStarts: true);
    await queue.SaveAsync(lease, saga, [], releaseLease: true, Token);
    Assert.Null((await repository.GetAsync(first.Id, Token))?.ActiveInstanceKey);

    var next = NewSaga(activeInstanceKey: "contract");
    await repository.CreateAsync(next, Created(next), Token);
    Assert.Equal("contract", (await repository.GetAsync(next.Id, Token))?.ActiveInstanceKey);
  }

  [Fact]
  public async Task A_repeated_correlation_id_is_reported_before_an_active_instance()
  {
    var (repository, _) = await CreateStoreAsync();
    var first = NewSaga(correlationId: "order-1", activeInstanceKey: "contract");
    await repository.CreateAsync(first, Created(first), Token);

    var repeat = NewSaga(correlationId: "order-1", activeInstanceKey: "contract");
    var error = await Assert.ThrowsAsync<DuplicateSagaException>(() => repository.CreateAsync(repeat, Created(repeat), Token));
    Assert.Equal(DuplicateSagaReason.CorrelationId, error.Reason);

    var another = NewSaga(correlationId: "order-2", activeInstanceKey: "contract");
    error = await Assert.ThrowsAsync<DuplicateSagaException>(() => repository.CreateAsync(another, Created(another), Token));
    Assert.Equal(DuplicateSagaReason.ActiveInstance, error.Reason);
    Assert.Equal(first.Id, error.ExistingSagaId);
  }

  [Fact]
  public async Task Claiming_returns_only_due_active_unleased_sagas_earliest_first()
  {
    var (repository, queue) = await CreateStoreAsync();
    var later = NewSaga(dueAt: Now.AddMinutes(-1));
    var earliest = NewSaga(dueAt: Now.AddMinutes(-5));
    var notDue = NewSaga(dueAt: Now.AddMinutes(1));
    var finished = NewSaga(dueAt: null, status: SagaStatus.Completed);
    foreach (var saga in new[] { later, earliest, notDue, finished })
      await repository.CreateAsync(saga, Created(saga), Token);

    var claimed = await queue.ClaimDueAsync(Claim(maxCount: 10), Token);

    Assert.Equal([earliest.Id, later.Id], claimed.Select(c => c.Saga.Id));
    Assert.All(claimed, c => Assert.Equal("worker-1", c.Lease.Owner));
    Assert.Empty(await queue.ClaimDueAsync(Claim(maxCount: 10), Token));
  }

  [Fact]
  public async Task Claiming_takes_at_most_the_requested_count()
  {
    var (repository, queue) = await CreateStoreAsync();
    for (var i = 0; i < 5; i++)
    {
      var saga = NewSaga(dueAt: Now.AddSeconds(-i));
      await repository.CreateAsync(saga, Created(saga), Token);
    }

    Assert.Equal(2, (await queue.ClaimDueAsync(Claim(maxCount: 2), Token)).Count);
    Assert.Equal(3, (await queue.ClaimDueAsync(Claim(maxCount: 10), Token)).Count);
  }

  [Fact]
  public async Task An_expired_lease_can_be_claimed_by_another_instance_and_the_old_lease_can_no_longer_save()
  {
    var (repository, queue) = await CreateStoreAsync();
    var saga = NewSaga();
    await repository.CreateAsync(saga, Created(saga), Token);
    var first = Assert.Single(await queue.ClaimDueAsync(Claim(leaseFor: TimeSpan.FromMinutes(1)), Token));

    var afterExpiry = Now.AddMinutes(2);
    var second = Assert.Single(await queue.ClaimDueAsync(new SagaClaimRequest("worker-2", 10, afterExpiry, afterExpiry.AddMinutes(5)), Token));
    Assert.NotEqual(first.Lease.Token, second.Lease.Token);

    await Assert.ThrowsAsync<SagaConcurrencyException>(() => queue.SaveAsync(first.Lease, first.Saga, [], true, Token));
    Assert.False(await queue.RenewLeaseAsync(first.Lease, afterExpiry.AddMinutes(10), Token));
    await queue.SaveAsync(second.Lease, second.Saga, [], true, Token);
  }

  [Fact]
  public async Task Saving_stores_the_saga_and_its_steps_appends_history_and_increments_the_version()
  {
    var (repository, queue) = await CreateStoreAsync();
    var created = NewSaga();
    await repository.CreateAsync(created, Created(created), Token);
    var (saga, lease) = Assert.Single(await queue.ClaimDueAsync(Claim(), Token));

    saga.Start(Now);
    saga.StartStep(0, "A", Now).Succeed(Now);
    saga.MoveToNextStep();
    var step = saga.StartStep(1, "B", Now);
    step.UpdateState("\"op\"");
    step.RecordCheck();
    saga.UpdateData("""{"trace":["A"]}""");
    saga.WaitUntil(Now);
    await queue.SaveAsync(lease, saga, [History(saga, SagaEventType.StepSucceeded, "A")], releaseLease: false, Token);
    Assert.Equal(1, saga.Version);

    // The lease is kept: the saga is due, but nobody else can claim it, and a second save works.
    Assert.Empty(await queue.ClaimDueAsync(Claim(at: Now.AddMinutes(1)), Token));
    step.RecordCheck();
    saga.WaitUntil(Now.AddHours(13));
    await queue.SaveAsync(lease, saga, [], releaseLease: true, Token);
    Assert.Equal(2, saga.Version);

    var stored = await repository.GetAsync(saga.Id, Token);
    Assert.NotNull(stored);
    Assert.Equal(SagaStatus.Waiting, stored.Status);
    Assert.Equal(1, stored.CurrentStepIndex);
    Assert.Equal(saga.DataJson, stored.DataJson);
    Assert.Equal(Now.AddHours(13), stored.NextRunAtUtc);
    Assert.Equal(2, stored.Version);
    Assert.Equal(["A", "B"], stored.Steps.Select(s => s.Name));
    Assert.Equal(2, stored.Steps[1].CheckCount);
    Assert.Equal("\"op\"", stored.Steps[1].StateJson);
    Assert.Equal([SagaEventType.Created, SagaEventType.StepSucceeded], (await repository.GetHistoryAsync(saga.Id, Token)).Select(h => h.EventType));

    // Released, so it can be claimed once due.
    Assert.Single(await queue.ClaimDueAsync(Claim(at: Now.AddHours(13)), Token));
  }

  [Fact]
  public async Task Requesting_cancellation_makes_the_saga_due_and_rejects_a_save_from_the_instance_running_it()
  {
    var (repository, queue) = await CreateStoreAsync();
    var created = NewSaga(dueAt: Now.AddMinutes(-1));
    await repository.CreateAsync(created, Created(created), Token);
    var (saga, lease) = Assert.Single(await queue.ClaimDueAsync(Claim(), Token));

    Assert.Equal(CancellationRequestResult.Requested, await repository.RequestCancellationAsync(saga.Id, Now, Token));
    Assert.Equal(CancellationRequestResult.AlreadyRequested, await repository.RequestCancellationAsync(saga.Id, Now, Token));
    await Assert.ThrowsAsync<SagaConcurrencyException>(() => queue.SaveAsync(lease, saga, [], true, Token));

    var stored = await repository.GetAsync(saga.Id, Token);
    Assert.NotNull(stored);
    Assert.True(stored.CancellationRequested);
    Assert.Equal(Now, stored.NextRunAtUtc);
  }

  [Fact]
  public async Task Cancellation_reports_missing_and_finished_sagas()
  {
    var (repository, _) = await CreateStoreAsync();
    var finished = NewSaga(dueAt: null, status: SagaStatus.Completed);
    await repository.CreateAsync(finished, Created(finished), Token);

    Assert.Equal(CancellationRequestResult.NotFound, await repository.RequestCancellationAsync(Guid.NewGuid(), Now, Token));
    Assert.Equal(CancellationRequestResult.NotCancellable, await repository.RequestCancellationAsync(finished.Id, Now, Token));
  }

  [Fact]
  public async Task A_lease_can_be_renewed_and_released()
  {
    var (repository, queue) = await CreateStoreAsync();
    var saga = NewSaga();
    await repository.CreateAsync(saga, Created(saga), Token);
    var claimed = Assert.Single(await queue.ClaimDueAsync(Claim(leaseFor: TimeSpan.FromMinutes(1)), Token));

    Assert.True(await queue.RenewLeaseAsync(claimed.Lease, Now.AddMinutes(10), Token));
    Assert.Empty(await queue.ClaimDueAsync(Claim(at: Now.AddMinutes(5)), Token));

    await queue.ReleaseLeaseAsync(claimed.Lease, Token);
    Assert.Single(await queue.ClaimDueAsync(Claim(at: Now.AddMinutes(5)), Token));
  }

  protected static CancellationToken Token => TestContext.Current.CancellationToken;

  protected static SagaRecord NewSaga(
    string? correlationId = null,
    string sagaType = "contract",
    DateTimeOffset? dueAt = null,
    SagaStatus status = SagaStatus.Pending,
    string? activeInstanceKey = null)
  {
    return SagaRecord.Restore(
      Guid.CreateVersion7(),
      sagaType,
      correlationId,
      activeInstanceKey,
      status,
      currentStepIndex: 0,
      dataJson: """{"trace":[]}""",
      createdAtUtc: Now,
      updatedAtUtc: Now,
      startedAtUtc: null,
      completedAtUtc: null,
      nextRunAtUtc: status.IsFinished() ? null : dueAt ?? Now,
      cancellationRequested: false,
      failureKind: null,
      failureStep: null,
      error: null,
      version: 0,
      steps: []);
  }

  protected static SagaClaimRequest Claim(int maxCount = 10, DateTimeOffset? at = null, TimeSpan? leaseFor = null)
  {
    var now = at ?? Now;
    return new SagaClaimRequest("worker-1", maxCount, now, now + (leaseFor ?? TimeSpan.FromMinutes(5)));
  }

  private static SagaHistoryEntry Created(SagaRecord saga) => History(saga, SagaEventType.Created, null);

  private static SagaHistoryEntry History(SagaRecord saga, SagaEventType type, string? step) =>
    new(saga.Id, saga.SagaType, Now, type, step, null);
}
