namespace NativeOrchestrator;

/// <summary>One step of a saga, as stored.</summary>
public sealed class StepRecord
{
  /// <summary>The step's position in the saga's declaration.</summary>
  public int StepIndex { get; set; }

  /// <summary>The step's name.</summary>
  public string Name { get; set; } = "";

  /// <summary>The step's status.</summary>
  public StepStatus Status { get; set; }

  /// <summary>How many times in a row the step has failed transiently. Reset when it succeeds or polls.</summary>
  public int Attempt { get; set; }

  /// <summary>For a polling step, how many times the operation has been checked.</summary>
  public int CheckCount { get; set; }

  /// <summary>How many times in a row the step's compensation has failed.</summary>
  public int CompensationAttempt { get; set; }

  /// <summary>State the step kept between runs (for a polling step, its operation), as JSON.</summary>
  public string? StateJson { get; set; }

  /// <summary>When the step first started.</summary>
  public DateTimeOffset StartedAtUtc { get; set; }

  /// <summary>When the step finished.</summary>
  public DateTimeOffset? CompletedAtUtc { get; set; }

  /// <summary>The last error, if any.</summary>
  public string? Error { get; set; }

  internal StepRecord Clone() => (StepRecord)MemberwiseClone();
}
