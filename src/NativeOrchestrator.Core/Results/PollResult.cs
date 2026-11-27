namespace NativeOrchestrator;

/// <summary>What a polling step's <c>CheckAsync</c> returns.</summary>
public readonly record struct PollResult
{
  private PollResult(PollState state, string? error, TimeSpan? retryAfter)
  {
    State = state;
    Error = error;
    RetryAfter = retryAfter;
  }

  /// <summary>Whether the operation is still running, succeeded or failed.</summary>
  public PollState State { get; }

  /// <summary>Why the operation failed, when <see cref="State"/> is <see cref="PollState.Failed"/>.</summary>
  public string? Error { get; }

  /// <summary>When to check again, if the external system said so. Otherwise the polling options decide.</summary>
  public TimeSpan? RetryAfter { get; }

  /// <summary>The operation is still running.</summary>
  /// <param name="retryAfter">When to check again, or <see langword="null"/> to use the polling options.</param>
  public static PollResult Pending(TimeSpan? retryAfter = null) => new(PollState.Pending, null, retryAfter);

  /// <summary>The operation finished successfully.</summary>
  public static PollResult Succeeded() => new(PollState.Succeeded, null, null);

  /// <summary>The operation failed permanently.</summary>
  /// <param name="error">Why it failed.</param>
  public static PollResult Failed(string error)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(error);
    return new PollResult(PollState.Failed, error, null);
  }
}
