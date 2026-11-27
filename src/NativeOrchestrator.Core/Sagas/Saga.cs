namespace NativeOrchestrator;

/// <summary>
/// The base class for a saga: a sequence of steps that call external systems, with compensation when one fails.
/// Derive from it, declare the steps in <see cref="Configure"/>, and register it with
/// <see cref="NativeOrchestratorBuilder.AddSaga{TSaga, TData}"/>.
/// </summary>
/// <typeparam name="TData">
/// The saga's data: its input, plus anything steps add for later steps. It is stored as JSON after each step, so keep
/// it serializable and small.
/// </typeparam>
/// <remarks>
/// One instance is created at startup to call <see cref="Configure"/>. The hooks run on an instance resolved from a DI
/// scope (or created with its constructor dependencies), so a saga can take services in its constructor.
/// </remarks>
public abstract class Saga<TData>
  where TData : class
{
  /// <summary>Declares the saga's steps, in the order they run.</summary>
  /// <param name="saga">The builder.</param>
  protected abstract void Configure(ISagaBuilder<TData> saga);

  /// <summary>
  /// Validates the data a saga is started with. Return an error message to reject the start (the API answers
  /// <c>400</c>), or <see langword="null"/> to accept it.
  /// </summary>
  /// <param name="data">The data.</param>
  protected virtual string? Validate(TData data) => null;

  /// <summary>Called once the saga has completed and been saved. Exceptions are logged, not retried.</summary>
  /// <param name="context">The saga.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  protected virtual ValueTask OnCompletedAsync(SagaContext<TData> context, CancellationToken cancellationToken) =>
    ValueTask.CompletedTask;

  /// <summary>
  /// Called once the saga has failed, finished compensating and been saved. <see cref="SagaContext{TData}.Status"/> is
  /// <see cref="SagaStatus.Compensated"/>, or <see cref="SagaStatus.Failed"/> if a compensation failed. Exceptions are
  /// logged, not retried.
  /// </summary>
  /// <param name="context">The saga.</param>
  /// <param name="failure">Why the saga failed.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  protected virtual ValueTask OnFailedAsync(SagaContext<TData> context, SagaFailure failure, CancellationToken cancellationToken) =>
    ValueTask.CompletedTask;

  internal void InvokeConfigure(ISagaBuilder<TData> saga) => Configure(saga);

  internal string? InvokeValidate(TData data) => Validate(data);

  internal ValueTask InvokeOnCompletedAsync(SagaContext<TData> context, CancellationToken cancellationToken) =>
    OnCompletedAsync(context, cancellationToken);

  internal ValueTask InvokeOnFailedAsync(SagaContext<TData> context, SagaFailure failure, CancellationToken cancellationToken) =>
    OnFailedAsync(context, failure, cancellationToken);
}
