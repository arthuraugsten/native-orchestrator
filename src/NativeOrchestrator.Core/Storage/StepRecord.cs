namespace NativeOrchestrator;

/// <summary>
/// One step of a saga, as stored. Only the engine changes it, through the record's own methods. A store that doesn't
/// use EF Core rebuilds a step from its stored values with <see cref="Restore"/>.
/// </summary>
public sealed class StepRecord
{
  private StepRecord()
  { }

  /// <summary>The step's position in the saga's declaration.</summary>
  public int StepIndex { get; private set; }

  /// <summary>The step's name.</summary>
  public string Name { get; private set; } = "";

  /// <summary>The step's status.</summary>
  public StepStatus Status { get; private set; }

  /// <summary>How many times in a row the step has failed transiently. Reset when it succeeds or polls.</summary>
  public int Attempt { get; private set; }

  /// <summary>For a polling step, how many times the operation has been checked.</summary>
  public int CheckCount { get; private set; }

  /// <summary>How many times in a row the step's compensation has failed.</summary>
  public int CompensationAttempt { get; private set; }

  /// <summary>State the step kept between runs (for a polling step, its operation), as JSON.</summary>
  public string? StateJson { get; private set; }

  /// <summary>When the step first started.</summary>
  public DateTimeOffset StartedAtUtc { get; private set; }

  /// <summary>When the step finished.</summary>
  public DateTimeOffset? CompletedAtUtc { get; private set; }

  /// <summary>The last error, if any.</summary>
  public string? Error { get; private set; }

  /// <summary>Rebuilds a step from the values a store persisted. Stores call it when they read a saga.</summary>
  /// <param name="stepIndex">The step's position in the saga's declaration.</param>
  /// <param name="name">The step's name.</param>
  /// <param name="status">The step's status.</param>
  /// <param name="attempt">How many times in a row the step has failed transiently.</param>
  /// <param name="checkCount">How many times the operation has been checked.</param>
  /// <param name="compensationAttempt">How many times in a row the step's compensation has failed.</param>
  /// <param name="stateJson">State the step kept between runs, as JSON.</param>
  /// <param name="startedAtUtc">When the step first started.</param>
  /// <param name="completedAtUtc">When the step finished.</param>
  /// <param name="error">The last error, if any.</param>
  public static StepRecord Restore(
    int stepIndex,
    string name,
    StepStatus status,
    int attempt,
    int checkCount,
    int compensationAttempt,
    string? stateJson,
    DateTimeOffset startedAtUtc,
    DateTimeOffset? completedAtUtc,
    string? error)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(name);

    return new StepRecord
    {
      StepIndex = stepIndex,
      Name = name,
      Status = status,
      Attempt = attempt,
      CheckCount = checkCount,
      CompensationAttempt = compensationAttempt,
      StateJson = stateJson,
      StartedAtUtc = startedAtUtc,
      CompletedAtUtc = completedAtUtc,
      Error = error,
    };
  }

  internal static StepRecord Start(int stepIndex, string name, DateTimeOffset nowUtc)
  {
    return new StepRecord
    {
      StepIndex = stepIndex,
      Name = name,
      Status = StepStatus.InProgress,
      StartedAtUtc = nowUtc,
    };
  }

  internal void UpdateState(string? stateJson) => StateJson = stateJson;

  /// <summary>The step succeeded: it is finished and its transient failures are forgotten.</summary>
  internal void Succeed(DateTimeOffset nowUtc)
  {
    Status = StepStatus.Succeeded;
    CompletedAtUtc = nowUtc;
    Attempt = 0;
    Error = null;
  }

  /// <summary>The operation was checked and isn't finished yet. Returns how many times it has been checked.</summary>
  internal int RecordCheck()
  {
    Attempt = 0;
    Error = null;
    CheckCount++;

    return CheckCount;
  }

  /// <summary>The step failed transiently. Returns how many times in a row it has.</summary>
  internal int RecordTransientFailure(string error)
  {
    Attempt++;
    Error = error;

    return Attempt;
  }

  /// <summary>The step failed for good.</summary>
  internal void Fail(string error, DateTimeOffset nowUtc)
  {
    Status = StepStatus.Failed;
    CompletedAtUtc = nowUtc;
    Error = error;
  }

  internal void MarkCompensated()
  {
    Status = StepStatus.Compensated;
    CompensationAttempt = 0;
  }

  /// <summary>The step's compensation failed. Returns how many times in a row it has.</summary>
  internal int RecordCompensationFailure(string error)
  {
    CompensationAttempt++;
    Error = error;

    return CompensationAttempt;
  }

  /// <summary>The step's compensation failed for good: it needs manual action.</summary>
  internal void MarkCompensationFailed(string error)
  {
    Status = StepStatus.CompensationFailed;
    Error = error;
  }

  internal StepRecord Clone() => (StepRecord)MemberwiseClone();
}
