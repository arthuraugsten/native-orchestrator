namespace NativeOrchestrator;

/// <summary>
/// What a step's <see cref="SagaStep{TData}.ExecuteAsync"/> returns. Throwing an exception means a <b>transient</b>
/// failure: the step is retried according to its <see cref="RetryOptions"/>.
/// </summary>
public sealed class StepResult
{
  private static readonly StepResult SuccessResult = new(StepResultKind.Success, null, null);
  private static readonly StepResult DefaultPollResult = new(StepResultKind.Poll, null, null);

  private StepResult(StepResultKind kind, TimeSpan? delay, string? error)
  {
    Kind = kind;
    Delay = delay;
    Error = error;
  }

  internal StepResultKind Kind { get; }

  internal TimeSpan? Delay { get; }

  internal string? Error { get; }

  /// <summary>The step finished. The saga moves to the next step.</summary>
  public static StepResult Success() => SuccessResult;

  /// <summary>
  /// The step failed permanently. It isn't retried: the saga compensates, unless the step has
  /// <see cref="StepOptions.ContinueOnError"/>.
  /// </summary>
  /// <param name="error">Why the step failed.</param>
  public static StepResult Failure(string error)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(error);
    return new StepResult(StepResultKind.Failure, null, error);
  }

  /// <summary>
  /// The step isn't finished: run it again later. Nothing waits in memory; the saga is saved with the time it is next
  /// due. The delay comes from the step's <see cref="PollingOptions"/>, unless <paramref name="delay"/> is given (for
  /// example, from an HTTP <c>Retry-After</c> header).
  /// </summary>
  /// <param name="delay">How long to wait, or <see langword="null"/> to use the polling options.</param>
  public static StepResult Poll(TimeSpan? delay = null)
  {
    if (delay is null)
      return DefaultPollResult;
    ArgumentOutOfRangeException.ThrowIfLessThan(delay.Value, TimeSpan.Zero);
    return new StepResult(StepResultKind.Poll, delay, null);
  }
}
