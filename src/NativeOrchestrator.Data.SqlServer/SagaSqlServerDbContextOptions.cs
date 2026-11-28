using Microsoft.EntityFrameworkCore;

namespace NativeOrchestrator.Data.SqlServer;

/// <summary>
/// Configures <see cref="SagaDbContext"/> for SQL Server 2025: the provider and compatibility level, these migrations,
/// timeouts and retries.
/// </summary>
internal static class SagaSqlServerDbContextOptions
{
  /// <summary>Configures the context options.</summary>
  /// <param name="builder">The context options builder.</param>
  /// <param name="options">The store options.</param>
  public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, SqlServerSagaStoreOptions options)
  {
    ArgumentNullException.ThrowIfNull(builder);
    ArgumentNullException.ThrowIfNull(options);

    return builder.UseSqlServer(options.ConnectionString, sql =>
    {
      sql.UseCompatibilityLevel(SagaModel.CompatibilityLevel);
      sql.MigrationsAssembly(typeof(SagaSqlServerDbContextOptions).Assembly.GetName().Name);
      sql.MigrationsHistoryTable(SagaModel.MigrationsHistoryTable, SagaModel.Schema);
      sql.CommandTimeout((int)Math.Ceiling(options.CommandTimeout.TotalSeconds));

      if (options.MaxRetryCount > 0)
        sql.EnableRetryOnFailure(options.MaxRetryCount, options.MaxRetryDelay, null);
    });
  }
}
