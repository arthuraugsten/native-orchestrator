using Microsoft.Extensions.DependencyInjection;
using NativeOrchestrator.Data.SqlServer;

namespace NativeOrchestrator;

/// <summary>Adds a health check for the SQL Server saga store.</summary>
public static class SqlServerHealthChecksBuilderExtensions
{
  /// <summary>
  /// Checks that the saga store's database can be reached. Requires <c>UseSqlServer</c> on the orchestrator builder.
  /// </summary>
  /// <param name="builder">The health checks builder.</param>
  /// <param name="name">The health check's name.</param>
  public static IHealthChecksBuilder AddSqlServerSagaStore(this IHealthChecksBuilder builder, string name = "saga-store")
  {
    ArgumentNullException.ThrowIfNull(builder);
    ArgumentException.ThrowIfNullOrWhiteSpace(name);

    return builder.AddDbContextCheck<SagaDbContext>(name);
  }
}
