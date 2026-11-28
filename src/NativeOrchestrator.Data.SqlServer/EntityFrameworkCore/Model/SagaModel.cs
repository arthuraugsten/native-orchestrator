namespace NativeOrchestrator.Data.SqlServer;

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

  /// <summary>SQL Server 2025's native JSON type, for saga data (always a JSON object).</summary>
  public const string JsonColumnType = "json";

  /// <summary>The compatibility level EF Core targets: SQL Server 2025 (17.x).</summary>
  public const int CompatibilityLevel = 170;
}
