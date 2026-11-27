using System.Text.Json;

namespace NativeOrchestrator;

/// <summary>Starts, reads and cancels sagas. Register it with <c>AddSagaOrchestrator</c> and inject it.</summary>
public interface ISagaClient
{
  /// <summary>
  /// Starts a saga with typed data. If <paramref name="correlationId"/> is given and a saga of the same type already
  /// has it, no saga is created and that saga's identifier is returned.
  /// </summary>
  /// <typeparam name="TData">The saga's data type.</typeparam>
  /// <param name="sagaType">The registered saga type name.</param>
  /// <param name="data">The data.</param>
  /// <param name="correlationId">An identifier from the caller that makes the start idempotent (at most 200 characters).</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  /// <exception cref="UnknownSagaTypeException">The saga type isn't registered.</exception>
  /// <exception cref="ArgumentException">The data is of the wrong type or the saga's <c>Validate</c> rejected it.</exception>
  Task<SagaStartResult> StartAsync<TData>(string sagaType, TData data, string? correlationId = null, CancellationToken cancellationToken = default)
    where TData : class;

  /// <summary>Starts a saga with data as JSON, for example from an HTTP request. Otherwise the same as the typed overload.</summary>
  /// <param name="sagaType">The registered saga type name.</param>
  /// <param name="data">The data as JSON.</param>
  /// <param name="correlationId">An identifier from the caller that makes the start idempotent (at most 200 characters).</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  /// <exception cref="UnknownSagaTypeException">The saga type isn't registered.</exception>
  /// <exception cref="ArgumentException">The data isn't valid for the saga.</exception>
  Task<SagaStartResult> StartAsync(string sagaType, JsonElement data, string? correlationId = null, CancellationToken cancellationToken = default);

  /// <summary>Reads a saga, or returns <see langword="null"/> if there is none.</summary>
  /// <param name="sagaId">The saga's identifier.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  Task<SagaSnapshot?> GetAsync(Guid sagaId, CancellationToken cancellationToken = default);

  /// <summary>Reads a saga's history, oldest first.</summary>
  /// <param name="sagaId">The saga's identifier.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  Task<IReadOnlyList<SagaHistoryEntry>> GetHistoryAsync(Guid sagaId, CancellationToken cancellationToken = default);

  /// <summary>
  /// Requests cancellation. The saga stops running its steps and compensates the succeeded ones the next time it runs,
  /// which the request makes immediate.
  /// </summary>
  /// <param name="sagaId">The saga's identifier.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  Task<CancellationRequestResult> CancelAsync(Guid sagaId, CancellationToken cancellationToken = default);
}
