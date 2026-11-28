using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NativeOrchestrator.Data.SqlServer;

namespace NativeOrchestrator;

/// <summary>Adds the SQL Server saga store.</summary>
public static class SqlServerSagaOrchestratorBuilderExtensions
{
  /// <summary>Stores sagas in SQL Server, through EF Core, with options set in code.</summary>
  /// <param name="builder">The orchestrator builder.</param>
  /// <param name="configure">Sets the options; at least <see cref="SqlServerSagaStoreOptions.ConnectionString"/>.</param>
  public static SagaOrchestratorBuilder UseSqlServer(this SagaOrchestratorBuilder builder, Action<SqlServerSagaStoreOptions> configure)
  {
    ArgumentNullException.ThrowIfNull(builder);
    ArgumentNullException.ThrowIfNull(configure);

    builder.Services.AddOptions<SqlServerSagaStoreOptions>().Configure(configure);

    return AddSqlServer(builder);
  }

  /// <summary>Stores sagas in SQL Server, through EF Core, with options bound from configuration.</summary>
  /// <param name="builder">The orchestrator builder.</param>
  /// <param name="configuration">The section with the <see cref="SqlServerSagaStoreOptions"/> values.</param>
  /// <param name="configure">Sets further options in code, after binding (for example the connection string).</param>
  public static SagaOrchestratorBuilder UseSqlServer(
    this SagaOrchestratorBuilder builder,
    IConfiguration configuration,
    Action<SqlServerSagaStoreOptions>? configure = null)
  {
    ArgumentNullException.ThrowIfNull(builder);
    ArgumentNullException.ThrowIfNull(configuration);

    var options = builder.Services.AddOptions<SqlServerSagaStoreOptions>().Bind(configuration);

    if (configure is not null)
      options.Configure(configure);

    return AddSqlServer(builder);
  }

  private static SagaOrchestratorBuilder AddSqlServer(SagaOrchestratorBuilder builder)
  {
    var services = builder.Services;

    services.AddOptions<SqlServerSagaStoreOptions>().ValidateOnStart();
    services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SqlServerSagaStoreOptions>, SqlServerSagaStoreOptionsValidator>());

    services.AddOptions<EntityFrameworkSagaStoreOptions>()
    .Configure<IOptions<SqlServerSagaStoreOptions>>((store, sql) => store.ApplyMigrationsOnStartup = sql.Value.ApplyMigrationsOnStartup);
    services.TryAddSingleton<ISagaClaimStrategy>(provider =>
      provider.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>().Value.UseLockingClaim
        ? new SqlServerClaimStrategy()
        : new DefaultClaimStrategy());

    return builder.UseEntityFrameworkCore((provider, dbContext) =>
      SagaSqlServerDbContextOptions.Configure(dbContext, provider.GetRequiredService<IOptions<SqlServerSagaStoreOptions>>().Value));
  }
}
