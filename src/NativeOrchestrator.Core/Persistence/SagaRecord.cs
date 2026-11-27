namespace NativeOrchestrator;

/// <summary>
/// A saga as stored. Stores persist it as it is; only the engine changes it. Leases (which instance is running the saga)
/// are kept by the store, not here.
/// </summary>
public sealed class SagaRecord
{
  /// <summary>The saga's identifier.</summary>
  public Guid Id { get; set; }

  /// <summary>The registered saga type name.</summary>
  public string SagaType { get; set; } = "";

  /// <summary>The caller's correlation identifier. Unique per saga type when set.</summary>
  public string? CorrelationId { get; set; }

  /// <summary>
  /// Set when only one saga with this key may be active at a time (the saga type, for a
  /// <see cref="SagaOptions.SingleInstance"/> saga); <see langword="null"/> otherwise. Stores keep it unique among the
  /// sagas that have one. The engine clears it when the saga completes or is compensated, so the next one can start. A
  /// <see cref="SagaStatus.Failed"/> saga keeps it until an operator clears it, unless
  /// <see cref="SagaOptions.FailedBlocksNewStarts"/> is off.
  /// </summary>
  public string? ActiveInstanceKey { get; set; }

  /// <summary>The saga's status.</summary>
  public SagaStatus Status { get; set; }

  /// <summary>The index of the step the saga runs next.</summary>
  public int CurrentStepIndex { get; set; }

  /// <summary>The saga's data, as JSON.</summary>
  public string DataJson { get; set; } = "{}";

  /// <summary>When the saga was created.</summary>
  public DateTimeOffset CreatedAtUtc { get; set; }

  /// <summary>When the saga was last saved.</summary>
  public DateTimeOffset UpdatedAtUtc { get; set; }

  /// <summary>When the first step started.</summary>
  public DateTimeOffset? StartedAtUtc { get; set; }

  /// <summary>When the saga finished.</summary>
  public DateTimeOffset? CompletedAtUtc { get; set; }

  /// <summary>When the saga is next due to run. <see langword="null"/> once it has finished.</summary>
  public DateTimeOffset? NextRunAtUtc { get; set; }

  /// <summary>Whether cancellation was requested.</summary>
  public bool CancellationRequested { get; set; }

  /// <summary>Why the saga failed, if it did.</summary>
  public SagaFailureKind? FailureKind { get; set; }

  /// <summary>The step that failed, if a step did.</summary>
  public string? FailureStep { get; set; }

  /// <summary>The failure's description.</summary>
  public string? Error { get; set; }

  /// <summary>
  /// The concurrency version. A store increments it on every save and rejects a save whose version doesn't match the
  /// stored one (<see cref="SagaConcurrencyException"/>).
  /// </summary>
  public int Version { get; set; }

  /// <summary>The steps that have started, in declaration order.</summary>
  public List<StepRecord> Steps { get; set; } = [];

  internal StepRecord? FindStep(int stepIndex)
  {
    foreach (var step in Steps)
    {
      if (step.StepIndex == stepIndex)
        return step;
    }

    return null;
  }

  internal SagaRecord Clone()
  {
    var clone = (SagaRecord)MemberwiseClone();
    clone.Steps = new List<StepRecord>(Steps.Count);
    foreach (var step in Steps)
      clone.Steps.Add(step.Clone());
    return clone;
  }
}
