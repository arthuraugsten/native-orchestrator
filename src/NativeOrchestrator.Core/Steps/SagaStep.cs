namespace NativeOrchestrator;

/// <summary>
/// The base class for a step. Steps are created from DI for each run, so they can take services (such as
/// <see cref="IHttpClientFactory"/>) in their constructors.
/// </summary>
/// <typeparam name="TData">The saga's data.</typeparam>
/// <remarks>
/// <para>
/// Steps run <b>at least once</b>: if an instance stops after a step called an external system but before the saga was
/// saved, the step runs again. Send <see cref="StepContext{TData}.IdempotencyKey"/> to external systems so they can
/// ignore the repeat.
/// </para>
/// <para>
/// Changes a step makes to <see cref="StepContext{TData}.Data"/> are saved when it returns
/// <see cref="StepResult.Success"/> or <see cref="StepResult.Poll"/>, and dropped when it fails or throws.
/// </para>
/// </remarks>
public abstract class SagaStep<TData>
  where TData : class
{
  /// <summary>
  /// Runs the step. Return <see cref="StepResult.Success"/> when it is done, <see cref="StepResult.Poll"/> to run it
  /// again later, or <see cref="StepResult.Failure"/> (or throw <see cref="StepFailedException"/>) to fail it
  /// permanently. Any other exception is a transient failure, and the step is retried.
  /// </summary>
  /// <param name="context">The step's context.</param>
  /// <param name="cancellationToken">Cancelled when the call takes longer than its execution timeout, the lease is lost or the host stops.</param>
  public abstract Task<StepResult> ExecuteAsync(StepContext<TData> context, CancellationToken cancellationToken);

  /// <summary>
  /// Undoes the step, after a later step failed or the saga was cancelled or timed out. Called only if the step
  /// succeeded. Throwing retries it according to <see cref="StepOptions.CompensationRetry"/>. Does nothing by default.
  /// </summary>
  /// <param name="context">The step's context.</param>
  /// <param name="cancellationToken">Cancelled when the call takes longer than its execution timeout, the lease is lost or the host stops.</param>
  public virtual Task CompensateAsync(StepContext<TData> context, CancellationToken cancellationToken) => Task.CompletedTask;
}
