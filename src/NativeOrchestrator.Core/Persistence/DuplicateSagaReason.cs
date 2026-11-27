namespace NativeOrchestrator;

/// <summary>Why <see cref="ISagaRepository.CreateAsync"/> rejected a saga.</summary>
public enum DuplicateSagaReason
{
  /// <summary>A saga of the same type with the same correlation identifier exists.</summary>
  CorrelationId,

  /// <summary>A saga with the same <see cref="SagaRecord.ActiveInstanceKey"/> is still active or failed.</summary>
  ActiveInstance,
}
