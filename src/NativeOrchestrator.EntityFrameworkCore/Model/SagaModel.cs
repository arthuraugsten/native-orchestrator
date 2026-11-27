namespace NativeOrchestrator.EntityFrameworkCore;

/// <summary>
/// Table, column and shadow property names of the saga store's model. Provider-specific SQL (such as a claim query)
/// uses them, so it stays in step with the mapping.
/// </summary>
internal static class SagaModel
{
  public const string Schema = "saga";
  public const string SagasTable = "Sagas";
  public const string StepsTable = "SagaSteps";
  public const string HistoryTable = "SagaHistory";
  public const string MigrationsHistoryTable = "__EFMigrationsHistory";

  // Shadow properties: kept by the store, not part of SagaRecord.
  public const string LeaseOwner = "LeaseOwner";
  public const string LeaseToken = "LeaseToken";
  public const string LeaseExpiresAtUtc = "LeaseExpiresAtUtc";
  public const string StepSagaId = "SagaId";
  public const string HistoryId = "Id";

  public const int SagaTypeLength = 100;
  public const int CorrelationIdLength = 200;
  public const int ActiveInstanceKeyLength = 200;
  public const int StepNameLength = 100;
  public const int OwnerLength = 200;
  public const int EnumLength = 32;
}
