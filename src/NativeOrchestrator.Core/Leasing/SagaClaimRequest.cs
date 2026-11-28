namespace NativeOrchestrator;

/// <summary>What <see cref="ISagaWorkQueue.ClaimDueAsync"/> claims.</summary>
/// <param name="Owner">The claiming instance.</param>
/// <param name="MaxCount">The most sagas to claim.</param>
/// <param name="NowUtc">The current time. A saga is due when its <see cref="SagaRecord.NextRunAtUtc"/> is at or before it.</param>
/// <param name="LeaseExpiresAtUtc">When the new leases expire.</param>
public sealed record SagaClaimRequest(string Owner, int MaxCount, DateTimeOffset NowUtc, DateTimeOffset LeaseExpiresAtUtc);
