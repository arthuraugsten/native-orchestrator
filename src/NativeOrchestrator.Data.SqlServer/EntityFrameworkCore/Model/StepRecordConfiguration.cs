using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NativeOrchestrator.Data.SqlServer;

internal sealed class StepRecordConfiguration : IEntityTypeConfiguration<StepRecord>
{
  public void Configure(EntityTypeBuilder<StepRecord> builder)
  {
    builder.ToTable(SagaModel.StepsTable, SagaModel.Schema);
    builder.Property<Guid>(SagaModel.StepSagaId);
    builder.HasKey(SagaModel.StepSagaId, nameof(StepRecord.StepIndex));
    builder.Property(s => s.StepIndex).ValueGeneratedNever();
    builder.Property(s => s.Name).HasMaxLength(SagaModel.StepNameLength).IsRequired();
    builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(SagaModel.EnumLength);
    // Not json: step state can be a top-level scalar (a string operation ID), which the json type rejects.
  }
}
