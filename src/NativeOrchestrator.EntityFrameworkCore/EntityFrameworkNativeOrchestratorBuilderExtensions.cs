using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NativeOrchestrator.EntityFrameworkCore;

namespace NativeOrchestrator;

/// <summary>Adds the EF Core saga store.</summary>
public static class EntityFrameworkNativeOrchestratorBuilderExtensions
{
  /// <summary>
  /// Stores sagas with EF Core. <paramref name="configureDbContext"/> picks the provider (for example
  /// <c>UseNpgsql</c>) and must set the migrations assembly. For SQL Server, use <c>UseSqlServer</c> from
  /// <c>NativeOrchestrator.SqlServer</c> instead, which does all of this.
  /// </summary>
  /// <param name="builder">The orchestrator builder.</param>
  /// <param name="configureDbContext">Configures the <see cref="SagaDbContext"/> options.</param>
  /// <param name="configure">Sets the store options.</param>
  public static NativeOrchestratorBuilder UseEntityFrameworkCore(
    this NativeOrchestratorBuilder builder,
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
