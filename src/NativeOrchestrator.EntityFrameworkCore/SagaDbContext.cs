using Microsoft.EntityFrameworkCore;

namespace NativeOrchestrator.EntityFrameworkCore;

/// <summary>
/// The saga store's database context: sagas, their steps and their history, in the <c>saga</c> schema. It has no
/// provider-specific mapping; a provider package (such as <c>NativeOrchestrator.SqlServer</c>) supplies the provider
/// and the migrations.
/// </summary>
public sealed class SagaDbContext : DbContext
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
