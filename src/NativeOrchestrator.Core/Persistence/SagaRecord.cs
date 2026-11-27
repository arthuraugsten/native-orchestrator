namespace NativeOrchestrator;

/// <summary>
/// A saga as stored. Stores persist it as it is; only the engine changes it, through the record's own methods. Leases
/// (which instance is running the saga) are kept by the store, not here. A store that doesn't use EF Core rebuilds a
/// record from its stored values with <see cref="Restore"/>.
/// </summary>
public sealed class SagaRecord
{
  private List<StepRecord> _steps = [];

  private SagaRecord()
  { }

  /// <summary>The saga's identifier.</summary>
  public Guid Id { get; private set; }

  /// <summary>The registered saga type name.</summary>
  public string SagaType { get; private set; } = "";

  /// <summary>The caller's correlation identifier. Unique per saga type when set.</summary>
  public string? CorrelationId { get; private set; }

  /// <summary>
  /// Set when only one saga with this key may be active at a time (the saga type, for a
  /// <see cref="SagaOptions.SingleInstance"/> saga); <see langword="null"/> otherwise. Stores keep it unique among the
  /// sagas that have one. The engine clears it when the saga completes or is compensated, so the next one can start. A
  /// <see cref="SagaStatus.Failed"/> saga keeps it until an operator clears it, unless
  /// <see cref="SagaOptions.FailedBlocksNewStarts"/> is off.
  /// </summary>
  public string? ActiveInstanceKey { get; private set; }

  /// <summary>The saga's status.</summary>
  public SagaStatus Status { get; private set; }

  /// <summary>The index of the step the saga runs next.</summary>
  public int CurrentStepIndex { get; private set; }

  /// <summary>The saga's data, as JSON.</summary>
  public string DataJson { get; private set; } = "{}";

  /// <summary>When the saga was created.</summary>
  public DateTimeOffset CreatedAtUtc { get; private set; }

  /// <summary>When the saga was last saved.</summary>
  public DateTimeOffset UpdatedAtUtc { get; private set; }

  /// <summary>When the first step started.</summary>
  public DateTimeOffset? StartedAtUtc { get; private set; }

  /// <summary>When the saga finished.</summary>
  public DateTimeOffset? CompletedAtUtc { get; private set; }

  /// <summary>When the saga is next due to run. <see langword="null"/> once it has finished.</summary>
  public DateTimeOffset? NextRunAtUtc { get; private set; }

  /// <summary>Whether cancellation was requested.</summary>
  public bool CancellationRequested { get; private set; }

  /// <summary>Why the saga failed, if it did.</summary>
  public SagaFailureKind? FailureKind { get; private set; }

  /// <summary>The step that failed, if a step did.</summary>
  public string? FailureStep { get; private set; }

  /// <summary>The failure's description.</summary>
  public string? Error { get; private set; }

  /// <summary>
  /// The concurrency version. A store increments it on every save (<see cref="IncrementVersion"/>) and rejects a save
  /// whose version doesn't match the stored one (<see cref="SagaConcurrencyException"/>).
  /// </summary>
  public int Version { get; private set; }

  /// <summary>The steps that have started, in declaration order.</summary>
  public IReadOnlyList<StepRecord> Steps => _steps;

  /// <summary>Rebuilds a saga from the values a store persisted. Stores call it when they read a saga.</summary>
  /// <param name="id">The saga's identifier.</param>
  /// <param name="sagaType">The registered saga type name.</param>
  /// <param name="correlationId">The caller's correlation identifier.</param>
  /// <param name="activeInstanceKey">The single-instance key, if the saga holds one.</param>
  /// <param name="status">The saga's status.</param>
  /// <param name="currentStepIndex">The index of the step the saga runs next.</param>
  /// <param name="dataJson">The saga's data, as JSON.</param>
  /// <param name="createdAtUtc">When the saga was created.</param>
  /// <param name="updatedAtUtc">When the saga was last saved.</param>
  /// <param name="startedAtUtc">When the first step started.</param>
  /// <param name="completedAtUtc">When the saga finished.</param>
  /// <param name="nextRunAtUtc">When the saga is next due to run.</param>
  /// <param name="cancellationRequested">Whether cancellation was requested.</param>
  /// <param name="failureKind">Why the saga failed, if it did.</param>
  /// <param name="failureStep">The step that failed, if a step did.</param>
  /// <param name="error">The failure's description.</param>
  /// <param name="version">The stored concurrency version.</param>
  /// <param name="steps">The steps that have started.</param>
  public static SagaRecord Restore(
    Guid id,
    string sagaType,
    string? correlationId,
    string? activeInstanceKey,
    SagaStatus status,
    int currentStepIndex,
    string dataJson,
    DateTimeOffset createdAtUtc,
    DateTimeOffset updatedAtUtc,
    DateTimeOffset? startedAtUtc,
    DateTimeOffset? completedAtUtc,
    DateTimeOffset? nextRunAtUtc,
    bool cancellationRequested,
    SagaFailureKind? failureKind,
    string? failureStep,
    string? error,
    int version,
    IEnumerable<StepRecord> steps)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(sagaType);
    ArgumentNullException.ThrowIfNull(dataJson);
    ArgumentNullException.ThrowIfNull(steps);

    return new SagaRecord
    {
      Id = id,
      SagaType = sagaType,
      CorrelationId = correlationId,
      ActiveInstanceKey = activeInstanceKey,
      Status = status,
      CurrentStepIndex = currentStepIndex,
      DataJson = dataJson,
      CreatedAtUtc = createdAtUtc,
      UpdatedAtUtc = updatedAtUtc,
      StartedAtUtc = startedAtUtc,
      CompletedAtUtc = completedAtUtc,
      NextRunAtUtc = nextRunAtUtc,
      CancellationRequested = cancellationRequested,
      FailureKind = failureKind,
      FailureStep = failureStep,
      Error = error,
      Version = version,
      _steps = steps.OrderBy(s => s.StepIndex).ToList(),
    };
  }

  /// <summary>
  /// Records a request to cancel the saga: it becomes due now and its version moves on, so a run in progress fails its
  /// next save and the next run compensates. Stores that change sagas in memory call it from
  /// <see cref="ISagaRepository.RequestCancellationAsync"/>.
  /// </summary>
  /// <param name="nowUtc">The current time.</param>
  /// <returns>
  /// <see cref="CancellationRequestResult.Requested"/>, or why nothing changed: the saga can't be cancelled in its status,
  /// or cancellation was already requested.
  /// </returns>
  public CancellationRequestResult RequestCancellation(DateTimeOffset nowUtc)
  {
    if (!SagaStatuses.Cancellable.Contains(Status))
      return CancellationRequestResult.NotCancellable;

    if (CancellationRequested)
      return CancellationRequestResult.AlreadyRequested;

    CancellationRequested = true;
    NextRunAtUtc = nowUtc;
    UpdatedAtUtc = nowUtc;
    Version++;

    return CancellationRequestResult.Requested;
  }

  /// <summary>Moves the concurrency version on. Stores call it after a save succeeds.</summary>
  public void IncrementVersion() => Version++;

  internal static SagaRecord Create(Guid id, string sagaType, string? correlationId, string? activeInstanceKey, string dataJson, DateTimeOffset nowUtc)
  {
    return new SagaRecord
    {
      Id = id,
      SagaType = sagaType,
      CorrelationId = correlationId,
      ActiveInstanceKey = activeInstanceKey,
      Status = SagaStatus.Pending,
      DataJson = dataJson,
      CreatedAtUtc = nowUtc,
      UpdatedAtUtc = nowUtc,
      NextRunAtUtc = nowUtc,
    };
  }

  /// <summary>Starts a pending saga. Returns <see langword="false"/> if it had already started.</summary>
  internal bool Start(DateTimeOffset nowUtc)
  {
    if (Status != SagaStatus.Pending)
      return false;

    Status = SagaStatus.Running;
    StartedAtUtc = nowUtc;

    return true;
  }

  /// <summary>Marks the saga as running its current step, due now in case the run is interrupted.</summary>
  internal void Resume(DateTimeOffset nowUtc)
  {
    Status = SagaStatus.Running;
    NextRunAtUtc = nowUtc;
  }

  /// <summary>Starts the step at <paramref name="stepIndex"/>, which must not have started before.</summary>
  internal StepRecord StartStep(int stepIndex, string name, DateTimeOffset nowUtc)
  {
    if (FindStep(stepIndex) is not null)
      throw new InvalidOperationException($"Step {stepIndex} of saga {Id} has already started.");

    var step = StepRecord.Start(stepIndex, name, nowUtc);

    AddStep(step);

    return step;
  }

  /// <summary>Adds a step as it is, keeping the steps in declaration order.</summary>
  internal void AddStep(StepRecord step)
  {
    var index = _steps.FindIndex(s => s.StepIndex > step.StepIndex);

    if (index < 0)
      _steps.Add(step);
    else
      _steps.Insert(index, step);
  }

  internal StepRecord? FindStep(int stepIndex)
  {
    foreach (var step in _steps)
    {
      if (step.StepIndex == stepIndex)
        return step;
    }

    return null;
  }

  /// <summary>The latest step that succeeded and isn't compensated yet: the next one to compensate.</summary>
  internal StepRecord? FindLatestSucceededStep()
  {
    StepRecord? latest = null;
    foreach (var step in _steps)
    {
      if (step.Status == StepStatus.Succeeded && (latest is null || step.StepIndex > latest.StepIndex))
        latest = step;
    }

    return latest;
  }

  internal bool AnyCompensationFailed()
  {
    foreach (var step in _steps)
    {
      if (step.Status == StepStatus.CompensationFailed)
        return true;
    }

    return false;
  }

  /// <summary>Moves on to the next declared step.</summary>
  internal void MoveToNextStep() => CurrentStepIndex++;

  internal void UpdateData(string dataJson) => DataJson = dataJson;

  /// <summary>Makes the saga due at <paramref name="dueAtUtc"/> without changing its status.</summary>
  internal void ScheduleAt(DateTimeOffset dueAtUtc) => NextRunAtUtc = dueAtUtc;

  /// <summary>
  /// Ends the run with the saga due at <paramref name="dueAtUtc"/>. A running saga becomes
  /// <see cref="SagaStatus.Waiting"/>; a compensating one stays <see cref="SagaStatus.Compensating"/>.
  /// </summary>
  internal void WaitUntil(DateTimeOffset dueAtUtc)
  {
    if (Status != SagaStatus.Compensating)
      Status = SagaStatus.Waiting;

    NextRunAtUtc = dueAtUtc;
  }

  /// <summary>
  /// Starts compensating: any step still in progress fails with <paramref name="error"/>, and the saga keeps why it
  /// failed and is due now.
  /// </summary>
  internal void BeginCompensation(SagaFailureKind kind, string? stepName, string error, DateTimeOffset nowUtc)
  {
    foreach (var step in _steps)
    {
      if (step.Status == StepStatus.InProgress)
        step.Fail(error, nowUtc);
    }

    Status = SagaStatus.Compensating;
    FailureKind = kind;
    FailureStep = stepName;
    Error = error;
    NextRunAtUtc = nowUtc;
  }

  /// <summary>
  /// Finishes the saga with <paramref name="status"/>. It frees its single-instance key, except that a
  /// <see cref="SagaStatus.Failed"/> saga keeps it while <paramref name="failedBlocksNewStarts"/> is on: it needs manual
  /// action, so new starts wait until it is resolved.
  /// </summary>
  internal void Finish(SagaStatus status, DateTimeOffset nowUtc, bool failedBlocksNewStarts)
  {
    Status = status;
    CompletedAtUtc = nowUtc;
    NextRunAtUtc = null;

    if (status != SagaStatus.Failed || !failedBlocksNewStarts)
      ActiveInstanceKey = null;
  }

  /// <summary>Records when the saga was last saved.</summary>
  internal void MarkUpdated(DateTimeOffset nowUtc) => UpdatedAtUtc = nowUtc;

  internal SagaRecord Clone()
  {
    var clone = (SagaRecord)MemberwiseClone();
    clone._steps = new List<StepRecord>(_steps.Count);
    foreach (var step in _steps)
      clone._steps.Add(step.Clone());
    return clone;
  }
}
