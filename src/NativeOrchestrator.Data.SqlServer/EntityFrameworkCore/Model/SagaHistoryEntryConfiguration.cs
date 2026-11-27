using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NativeOrchestrator.Data.SqlServer;

internal sealed class SagaHistoryEntryConfiguration : IEntityTypeConfiguration<SagaHistoryEntry>
{
  public void Configure(EntityTypeBuilder<SagaHistoryEntry> builder)
  {
    builder.ToTable(SagaModel.HistoryTable, SagaModel.Schema);
    builder.Property<long>(SagaModel.HistoryId).ValueGeneratedOnAdd();
    builder.HasKey(SagaModel.HistoryId);
    builder.Property(h => h.SagaType).HasMaxLength(SagaModel.SagaTypeLength).IsRequired();
    builder.Property(h => h.EventType).HasConversion<string>().HasMaxLength(SagaModel.EnumLength);
    builder.Property(h => h.StepName).HasMaxLength(SagaModel.StepNameLength);

    builder.HasOne<SagaRecord>()
      .WithMany()
      .HasForeignKey(h => h.SagaId)
      .OnDelete(DeleteBehavior.Cascade);
    builder.HasIndex(h => h.SagaId);
  }
}
