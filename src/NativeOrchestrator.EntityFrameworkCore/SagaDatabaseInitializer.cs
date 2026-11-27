using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace NativeOrchestrator.EntityFrameworkCore;

/// <summary>Applies pending migrations before the worker starts, when <see cref="EntityFrameworkSagaStoreOptions.ApplyMigrationsOnStartup"/> is set.</summary>
internal sealed class SagaDatabaseInitializer : ISagaStoreInitializer
{
  private readonly IDbContextFactory<SagaDbContext> _contexts;
  private readonly EntityFrameworkSagaStoreOptions _options;

  public SagaDatabaseInitializer(IDbContextFactory<SagaDbContext> contexts, IOptions<EntityFrameworkSagaStoreOptions> options)
  {
    _contexts = contexts;
    _options = options.Value;
  }

  public async Task InitializeAsync(CancellationToken cancellationToken)
  {
    if (!_options.ApplyMigrationsOnStartup)
      return;
    await using var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
  }
}
