namespace NativeOrchestrator.EntityFrameworkCore;

/// <summary>
/// Sets a lease on due sagas, in one statement. The store then reads back the sagas that carry
/// <c>leaseToken</c>. Replace it with a provider-specific query (row locks that skip locked rows) for many instances.
/// </summary>
public interface ISagaClaimStrategy
{
  /// <summary>Leases up to <see cref="SagaClaimRequest.MaxCount"/> due sagas whose lease is free or expired, earliest due first.</summary>
  /// <param name="context">The context to run the statement on.</param>
  /// <param name="request">What to claim.</param>
  /// <param name="leaseToken">The token to set on the claimed sagas.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  /// <returns>How many sagas were leased.</returns>
  Task<int> ClaimAsync(SagaDbContext context, SagaClaimRequest request, Guid leaseToken, CancellationToken cancellationToken);
}
