namespace NativeOrchestrator;

/// <summary>A read-only view of one step of a saga.</summary>
/// <param name="Name">The step's name.</param>
/// <param name="Status">The step's status.</param>
/// <param name="Attempt">How many times in a row the step has failed transiently (0 if it hasn't).</param>
/// <param name="CheckCount">For a polling step, how many times the operation has been checked.</param>
/// <param name="StartedAtUtc">When the step first started.</param>
/// <param name="CompletedAtUtc">When the step finished.</param>
/// <param name="Error">The last error, if any.</param>
public sealed record StepSnapshot(
  string Name,
  StepStatus Status,
  int Attempt,
  int CheckCount,
  DateTimeOffset StartedAtUtc,
  DateTimeOffset? CompletedAtUtc,
  string? Error);
