namespace NativeOrchestrator;

/// <summary>
/// Hands due sagas to workers and saves their progress. Leases make sure only one instance runs a saga at a time;
/// the version check makes sure a stale save never overwrites a newer one.
/// </summary>
public interface ISagaWorkQueue
{
  /// <summary>
  /// Claims up to <see cref="SagaClaimRequest.MaxCount"/> sagas whose status is in <see cref="SagaStatuses.Active"/>,
  /// that are due, and whose lease is free or expired, earliest due first. Each gets a new lease.
  /// </summary>
  /// <param name="request">What to claim.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  Task<IReadOnlyList<ClaimedSaga>> ClaimDueAsync(SagaClaimRequest request, CancellationToken cancellationToken);

  /// <summary>Extends a lease. Returns <see langword="false"/> if the lease was lost (another instance claimed the saga).</summary>
  /// <param name="lease">The lease.</param>
  /// <param name="expiresAtUtc">The new expiry time.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  Task<bool> RenewLeaseAsync(SagaLease lease, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken);

  /// <summary>
  /// Saves a claimed saga (and its steps) and appends history entries, in one transaction, then increments
  /// <see cref="SagaRecord.Version"/> on <paramref name="saga"/>.
  /// </summary>
  /// <param name="lease">The lease the saga was claimed with.</param>
  /// <param name="saga">The saga, with the version it was read with.</param>
  /// <param name="history">History entries to append.</param>
  /// <param name="releaseLease">Whether to release the lease, or keep it for a further save.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  /// <exception cref="SagaConcurrencyException">The lease was lost, or the saga changed since it was read.</exception>
  Task SaveAsync(SagaLease lease, SagaRecord saga, IReadOnlyList<SagaHistoryEntry> history, bool releaseLease, CancellationToken cancellationToken);

  /// <summary>Releases a lease without saving, if it is still held.</summary>
  /// <param name="lease">The lease.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  Task ReleaseLeaseAsync(SagaLease lease, CancellationToken cancellationToken);
}
