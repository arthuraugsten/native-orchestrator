namespace NativeOrchestrator;

/// <summary>The default <see cref="IRetryPolicy"/>: retries every exception until <see cref="RetryOptions.MaxAttempts"/>, with the <see cref="IBackoffStrategy"/> delay.</summary>
public sealed class DefaultRetryPolicy : IRetryPolicy
{
  private readonly IBackoffStrategy _backoff;

  /// <summary>Creates the policy.</summary>
  /// <param name="backoff">Computes the delays.</param>
  public DefaultRetryPolicy(IBackoffStrategy backoff)
  {
    ArgumentNullException.ThrowIfNull(backoff);
    _backoff = backoff;
  }

  /// <inheritdoc />
  public RetryDecision Evaluate(RetryContext context) =>
    context.FailureCount < context.Options.MaxAttempts
      ? RetryDecision.After(_backoff.GetDelay(context.FailureCount, context.Options))
      : RetryDecision.Stop;
}
