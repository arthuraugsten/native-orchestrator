namespace NativeOrchestrator;

/// <summary>A claim on a saga: while it holds, only its owner may save the saga.</summary>
/// <param name="SagaId">The saga's identifier.</param>
/// <param name="Token">A value unique to this claim. A store accepts a save only while the saga still has this token.</param>
/// <param name="Owner">The instance that holds the lease (<see cref="SagaOrchestratorOptions.InstanceName"/>).</param>
public sealed record SagaLease(Guid SagaId, Guid Token, string Owner);
