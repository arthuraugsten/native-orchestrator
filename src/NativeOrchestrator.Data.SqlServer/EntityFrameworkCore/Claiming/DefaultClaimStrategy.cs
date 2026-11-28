using Microsoft.EntityFrameworkCore;

namespace NativeOrchestrator.Data.SqlServer;

/// <summary>
/// A claim that works on any relational provider: one <c>UPDATE ... WHERE Id IN (SELECT TOP n ...)</c> built with
/// <c>ExecuteUpdateAsync</c>.
/// </summary>
/// <remarks>
/// Under heavy contention two instances can both lease the same saga; the second lease replaces the first. That is
/// safe, because a save only succeeds with the current lease token, so the first instance's save is rejected. It costs
/// a repeated step run, which steps tolerate because they run at least once. Provider packages supply a claim that
/// locks rows instead.
/// </remarks>
internal sealed class DefaultClaimStrategy : ISagaClaimStrategy
{
  private static readonly SagaStatus[] Active = [.. SagaStatuses.Active];

  /// <inheritdoc />
  public Task<int> ClaimAsync(SagaDbContext context, SagaClaimRequest request, Guid leaseToken, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(request);

    var now = request.NowUtc;

    return context.Sagas
      .Where(s => Active.Contains(s.Status)
        && s.NextRunAtUtc <= now
        && (EF.Property<DateTimeOffset?>(s, SagaModel.LeaseExpiresAtUtc) == null
          || EF.Property<DateTimeOffset?>(s, SagaModel.LeaseExpiresAtUtc) < now))
      .OrderBy(s => s.NextRunAtUtc)
      .Take(request.MaxCount)
      .ExecuteUpdateAsync(
        set => set
          .SetProperty(s => EF.Property<Guid?>(s, SagaModel.LeaseToken), leaseToken)
          .SetProperty(s => EF.Property<string?>(s, SagaModel.LeaseOwner), request.Owner)
          .SetProperty(s => EF.Property<DateTimeOffset?>(s, SagaModel.LeaseExpiresAtUtc), request.LeaseExpiresAtUtc),
        cancellationToken);
  }
}
