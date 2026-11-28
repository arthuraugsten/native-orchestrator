namespace NativeOrchestrator;

/// <summary>What <see cref="IRetryPolicy"/> decided.</summary>
/// <param name="ShouldRetry">Whether to retry.</param>
/// <param name="Delay">How long to wait before retrying.</param>
public readonly record struct RetryDecision(bool ShouldRetry, TimeSpan Delay)
{
  /// <summary>Don't retry: the step (or compensation) fails.</summary>
  public static RetryDecision Stop => new(false, TimeSpan.Zero);

  /// <summary>Retry after a delay.</summary>
  /// <param name="delay">How long to wait.</param>
  public static RetryDecision After(TimeSpan delay) => new(true, delay);
}
