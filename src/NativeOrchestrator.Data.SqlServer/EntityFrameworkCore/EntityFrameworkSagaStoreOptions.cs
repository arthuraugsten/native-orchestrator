namespace NativeOrchestrator.Data.SqlServer;

/// <summary>Options for the EF Core saga store.</summary>
internal sealed class EntityFrameworkSagaStoreOptions
{
  /// <summary>
  /// Whether the worker applies pending migrations before it starts. Convenient in development; in production, apply
  /// them in the deployment (<c>dotnet ef migrations bundle</c> or a SQL script) instead.
  /// </summary>
  public bool ApplyMigrationsOnStartup { get; set; }
}
