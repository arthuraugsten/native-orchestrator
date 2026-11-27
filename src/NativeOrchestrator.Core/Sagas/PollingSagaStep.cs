namespace NativeOrchestrator;

/// <summary>
/// The base class for a step that starts a long-running operation in an external system, then polls until it finishes.
/// The engine saves the operation after <see cref="StartAsync"/> and between checks, so polling survives restarts and
/// can last days. How often it checks comes from <see cref="PollingOptions"/>; how long it may take, from
/// <see cref="StepOptions.Timeout"/>.
/// </summary>
/// <typeparam name="TData">The saga's data.</typeparam>
/// <typeparam name="TOperation">What identifies the operation (a job ID, a status URL), saved between checks.</typeparam>
/// <remarks>
/// If a check throws, it is retried according to <see cref="RetryOptions"/>, and the operation is kept, so
/// <see cref="StartAsync"/> doesn't run again. To fail permanently, return <see cref="PollResult.Failed"/> from a
/// check, or throw <see cref="StepFailedException"/> from <see cref="StartAsync"/>.
/// </remarks>
public abstract class PollingSagaStep<TData, TOperation> : SagaStep<TData>
  where TData : class
  where TOperation : notnull
{
  /// <summary>Starts the operation. Send <see cref="StepContext{TData}.IdempotencyKey"/>, so a repeat start is ignored.</summary>
  /// <param name="context">The step's context.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  protected abstract Task<TOperation> StartAsync(StepContext<TData> context, CancellationToken cancellationToken);

  /// <summary>Checks whether the operation has finished. On success, copy any result into <see cref="StepContext{TData}.Data"/>.</summary>
  /// <param name="context">The step's context.</param>
  /// <param name="operation">The operation <see cref="StartAsync"/> returned.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  protected abstract Task<PollResult> CheckAsync(StepContext<TData> context, TOperation operation, CancellationToken cancellationToken);

  /// <summary>Starts the operation on the first run, then checks it on each later run.</summary>
  /// <param name="context">The step's context.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  public sealed override async Task<StepResult> ExecuteAsync(StepContext<TData> context, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(context);
    if (!context.TryGetState<TOperation>(out var operation))
    {
      operation = await StartAsync(context, cancellationToken).ConfigureAwait(false);
      context.SetState(operation);
      return StepResult.Poll();
    }

    var result = await CheckAsync(context, operation, cancellationToken).ConfigureAwait(false);
    return result.State switch
    {
      PollState.Succeeded => StepResult.Success(),
      PollState.Failed => StepResult.Failure(result.Error ?? "The operation failed."),
      _ => StepResult.Poll(result.RetryAfter),
    };
  }
}
