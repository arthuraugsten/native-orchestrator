namespace NativeOrchestrator;

/// <summary>Computes the delay before an attempt, for retries and polls. Replace it with <see cref="SagaOrchestratorBuilder.UseBackoffStrategy{TStrategy}"/>.</summary>
public interface IBackoffStrategy
{
  /// <summary>The delay before the next attempt.</summary>
  /// <param name="attempt">How many attempts have been made so far (1 after the first).</param>
  /// <param name="options">The backoff settings.</param>
  TimeSpan GetDelay(int attempt, BackoffOptions options);
}
