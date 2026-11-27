namespace NativeOrchestrator;

/// <summary>
/// Thrown by <see cref="ISagaRepository.CreateAsync"/> when a saga of the same type with the same correlation identifier
/// exists, or when another saga with the same <see cref="SagaRecord.ActiveInstanceKey"/> is active.
/// </summary>
public sealed class DuplicateSagaException : Exception
{
  /// <summary>Creates the exception.</summary>
  /// <param name="existingSagaId">The identifier of the saga that already exists.</param>
  /// <param name="reason">Why the saga was rejected.</param>
  /// <param name="innerException">The store's exception, if any.</param>
  public DuplicateSagaException(Guid existingSagaId, DuplicateSagaReason reason = DuplicateSagaReason.CorrelationId, Exception? innerException = null)
    : base(
      reason == DuplicateSagaReason.ActiveInstance
        ? $"A saga of the same type is already active: {existingSagaId}."
        : $"A saga with the same type and correlation identifier already exists: {existingSagaId}.",
      innerException)
  {
    ExistingSagaId = existingSagaId;
    Reason = reason;
  }

  /// <summary>The identifier of the saga that already exists.</summary>
  public Guid ExistingSagaId { get; }

  /// <summary>Why the saga was rejected.</summary>
  public DuplicateSagaReason Reason { get; }
}
