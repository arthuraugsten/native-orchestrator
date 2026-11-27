using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NativeOrchestrator.EntityFrameworkCore;

internal sealed class SagaRecordConfiguration : IEntityTypeConfiguration<SagaRecord>
{
  public void Configure(EntityTypeBuilder<SagaRecord> builder)
  {
    builder.ToTable(SagaModel.SagasTable, SagaModel.Schema);
    builder.HasKey(s => s.Id);
    builder.Property(s => s.Id).ValueGeneratedNever();
    builder.Property(s => s.SagaType).HasMaxLength(SagaModel.SagaTypeLength).IsRequired();
    builder.Property(s => s.CorrelationId).HasMaxLength(SagaModel.CorrelationIdLength);
    builder.Property(s => s.ActiveInstanceKey).HasMaxLength(SagaModel.ActiveInstanceKeyLength);
    builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(SagaModel.EnumLength);
    builder.Property(s => s.DataJson).IsRequired();
    builder.Property(s => s.FailureKind).HasConversion<string>().HasMaxLength(SagaModel.EnumLength);
    builder.Property(s => s.FailureStep).HasMaxLength(SagaModel.StepNameLength);
    builder.Property(s => s.Version).IsConcurrencyToken();

    builder.Property<string?>(SagaModel.LeaseOwner).HasMaxLength(SagaModel.OwnerLength);
    builder.Property<Guid?>(SagaModel.LeaseToken);
    builder.Property<DateTimeOffset?>(SagaModel.LeaseExpiresAtUtc);

    builder.HasMany(s => s.Steps)
      .WithOne()
      .HasForeignKey(SagaModel.StepSagaId)
      .OnDelete(DeleteBehavior.Cascade);
    builder.Navigation(s => s.Steps).AutoInclude(false);

    // Idempotent starts: one saga per type and correlation ID (null correlation IDs aren't constrained).
    builder.HasIndex(s => new { s.SagaType, s.CorrelationId }).IsUnique();
    // Single-instance sagas: one active saga per key (null keys aren't constrained; finished sagas have none).
    builder.HasIndex(s => s.ActiveInstanceKey).IsUnique();
    // Claiming: due sagas by status and due time.
    builder.HasIndex(s => new { s.Status, s.NextRunAtUtc });
  }
}
