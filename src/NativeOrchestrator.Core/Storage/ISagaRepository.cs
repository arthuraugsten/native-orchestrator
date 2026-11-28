namespace NativeOrchestrator;

/// <summary>Creates and reads sagas. Implemented by a store, with <see cref="ISagaWorkQueue"/>.</summary>
public interface ISagaRepository
{
  /// <summary>Stores a new saga and its first history entry.</summary>
  /// <param name="saga">The saga. Its <see cref="SagaRecord.Version"/> is 0.</param>
  /// <param name="created">The saga's <see cref="SagaEventType.Created"/> history entry.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  /// <exception cref="DuplicateSagaException">
  /// A saga of the same type with the same correlation identifier exists (checked first), or another saga with the same
  /// <see cref="SagaRecord.ActiveInstanceKey"/> exists. <see cref="DuplicateSagaException.Reason"/> says which.
  /// </exception>
  Task CreateAsync(SagaRecord saga, SagaHistoryEntry created, CancellationToken cancellationToken);

  /// <summary>Reads a saga and its steps, or returns <see langword="null"/> if there is none.</summary>
  /// <param name="sagaId">The saga's identifier.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  Task<SagaRecord?> GetAsync(Guid sagaId, CancellationToken cancellationToken);

  /// <summary>Reads a saga's history, oldest first.</summary>
  /// <param name="sagaId">The saga's identifier.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  Task<IReadOnlyList<SagaHistoryEntry>> GetHistoryAsync(Guid sagaId, CancellationToken cancellationToken);

  /// <summary>
  /// Requests cancellation of a saga whose status is in <see cref="SagaStatuses.Cancellable"/>: sets
  /// <see cref="SagaRecord.CancellationRequested"/>, makes the saga due at <paramref name="nowUtc"/> and increments its
  /// version, so a save by an instance that is running the saga fails and the cancellation is seen next.
  /// </summary>
  /// <param name="sagaId">The saga's identifier.</param>
  /// <param name="nowUtc">The current time.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  Task<CancellationRequestResult> RequestCancellationAsync(Guid sagaId, DateTimeOffset nowUtc, CancellationToken cancellationToken);
}
