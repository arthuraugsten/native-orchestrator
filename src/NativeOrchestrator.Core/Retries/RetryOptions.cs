namespace NativeOrchestrator;

/// <summary>
/// How a step is retried after a transient failure (an exception). Retries are durable: the saga is saved with the time
/// of the next attempt, so they survive restarts and can be minutes or hours apart.
/// </summary>
public sealed class RetryOptions : BackoffOptions
{
  /// <summary>Creates retry options with the defaults: 5 attempts, from 10 seconds doubling to 10 minutes, with jitter.</summary>
  public RetryOptions()
  {
    UseJitter = true;
  }

  /// <summary>The most attempts, including the first. 1 means no retries.</summary>
  public int MaxAttempts { get; set; } = 5;
}
