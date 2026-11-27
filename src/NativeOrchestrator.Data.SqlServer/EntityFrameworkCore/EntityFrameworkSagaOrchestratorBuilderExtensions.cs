using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NativeOrchestrator.Data.SqlServer;

/// <summary>
/// Adds the EF Core saga store. EF Core is how this provider reaches SQL Server, not part of the public API:
/// <c>UseSqlServer</c> calls this.
/// </summary>
internal static class EntityFrameworkSagaOrchestratorBuilderExtensions
{
  /// <summary>
  /// Stores sagas with EF Core. <paramref name="configureDbContext"/> picks the database provider and must set the
  /// migrations assembly.
  /// </summary>
  public static SagaOrchestratorBuilder UseEntityFrameworkCore(
    this SagaOrchestratorBuilder builder,
    Action<IServiceProvider, DbContextOptionsBuilder> configureDbContext,
    Action<EntityFrameworkSagaStoreOptions>? configure = null)
  {
    ArgumentNullException.ThrowIfNull(builder);
    ArgumentNullException.ThrowIfNull(configureDbContext);
    var services = builder.Services;
    services.AddDbContextFactory<SagaDbContext>(configureDbContext);
    services.AddOptions<EntityFrameworkSagaStoreOptions>();
    if (configure is not null)
      services.Configure(configure);
    services.TryAddSingleton<ISagaClaimStrategy, DefaultClaimStrategy>();
    services.TryAddEnumerable(ServiceDescriptor.Singleton<ISagaStoreInitializer, SagaDatabaseInitializer>());
    return builder.UseStore<EntityFrameworkSagaStore>();
  }
}
