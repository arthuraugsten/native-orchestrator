namespace NativeOrchestrator;

/// <summary>
/// Decides whether a step (or a compensation) that threw is retried, and when. Replace it with
/// <see cref="SagaOrchestratorBuilder.UseRetryPolicy{TPolicy}"/>, for example to stop retrying some exception types.
/// </summary>
public interface IRetryPolicy
{
  /// <summary>Decides whether to retry.</summary>
  /// <param name="context">The failure.</param>
  RetryDecision Evaluate(RetryContext context);
}
