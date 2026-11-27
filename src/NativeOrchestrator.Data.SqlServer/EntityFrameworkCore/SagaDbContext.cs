using Microsoft.EntityFrameworkCore;

namespace NativeOrchestrator.Data.SqlServer;

/// <summary>
/// The saga store's database context: sagas, their steps and their history, in the <c>saga</c> schema. The mapping has
/// nothing specific to SQL Server; <see cref="SagaSqlServerDbContextOptions"/> supplies the provider and the migrations.
/// </summary>
internal sealed class SagaDbContext : DbContext
{
  /// <summary>Creates the context.</summary>
  /// <param name="options">The context options.</param>
  public SagaDbContext(DbContextOptions<SagaDbContext> options)
    : base(options)
  {
  }

  /// <summary>Sagas, with their leases as shadow properties.</summary>
  public DbSet<SagaRecord> Sagas => Set<SagaRecord>();

  /// <summary>Saga steps.</summary>
  public DbSet<StepRecord> Steps => Set<StepRecord>();

  /// <summary>Saga history, append-only.</summary>
  public DbSet<SagaHistoryEntry> History => Set<SagaHistoryEntry>();

  /// <inheritdoc />
  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    ArgumentNullException.ThrowIfNull(modelBuilder);
    modelBuilder.HasDefaultSchema(SagaModel.Schema);
    modelBuilder.ApplyConfiguration(new SagaRecordConfiguration());
    modelBuilder.ApplyConfiguration(new StepRecordConfiguration());
    modelBuilder.ApplyConfiguration(new SagaHistoryEntryConfiguration());
  }
}
