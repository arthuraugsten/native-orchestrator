using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace NativeOrchestrator.Data.SqlServer;

/// <summary>
/// Claims due sagas with row locks: <c>UPDLOCK</c> keeps two instances from leasing the same row, and
/// <c>READPAST</c> lets each skip the rows another is claiming instead of waiting for them.
/// </summary>
internal sealed class SqlServerClaimStrategy : ISagaClaimStrategy
{
  private static readonly string ClaimSql = BuildClaimSql();

  /// <inheritdoc />
  public Task<int> ClaimAsync(SagaDbContext context, SagaClaimRequest request, Guid leaseToken, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(request);
    return context.Database.ExecuteSqlRawAsync(
      ClaimSql,
      [
        new SqlParameter("@maxCount", request.MaxCount),
        new SqlParameter("@now", request.NowUtc),
        new SqlParameter("@owner", request.Owner),
        new SqlParameter("@token", leaseToken),
        new SqlParameter("@expiresAt", request.LeaseExpiresAtUtc),
      ],
      cancellationToken);
  }

  private static string BuildClaimSql()
  {
    // Status values are enum names stored as strings, so they are safe to inline.
    var active = string.Join(", ", SagaStatuses.Active.Select(status => $"N'{status}'"));
    return $"""
      WITH due AS (
        SELECT TOP (@maxCount) [{SagaModel.LeaseOwner}], [{SagaModel.LeaseToken}], [{SagaModel.LeaseExpiresAtUtc}]
        FROM [{SagaModel.Schema}].[{SagaModel.SagasTable}] WITH (UPDLOCK, READPAST, ROWLOCK)
        WHERE [{nameof(SagaRecord.Status)}] IN ({active})
          AND [{nameof(SagaRecord.NextRunAtUtc)}] <= @now
          AND ([{SagaModel.LeaseExpiresAtUtc}] IS NULL OR [{SagaModel.LeaseExpiresAtUtc}] < @now)
        ORDER BY [{nameof(SagaRecord.NextRunAtUtc)}])
      UPDATE due
      SET [{SagaModel.LeaseOwner}] = @owner, [{SagaModel.LeaseToken}] = @token, [{SagaModel.LeaseExpiresAtUtc}] = @expiresAt;
      """;
  }
}
