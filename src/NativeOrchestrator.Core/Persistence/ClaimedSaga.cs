namespace NativeOrchestrator;

/// <summary>A saga claimed by <see cref="ISagaWorkQueue.ClaimDueAsync"/>, with its lease.</summary>
/// <param name="Saga">The saga, including its steps.</param>
/// <param name="Lease">The lease the claim took.</param>
public sealed record ClaimedSaga(SagaRecord Saga, SagaLease Lease);
