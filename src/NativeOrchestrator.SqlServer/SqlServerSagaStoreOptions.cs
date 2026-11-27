namespace NativeOrchestrator.SqlServer;

/// <summary>Options for the SQL Server saga store. Bind them from configuration or set them in code.</summary>
public sealed class SqlServerSagaStoreOptions
{
  /// <summary>The connection string. Required.</summary>
  public string ConnectionString { get; set; } = "";

  /// <summary>The command timeout. Defaults to 30 seconds.</summary>
  public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);

  /// <summary>
  /// How many times EF Core retries a command after a transient SQL Server error (deadlock, failover, throttling).
  /// 0 turns retries off. Defaults to 6.
  /// </summary>
  public int MaxRetryCount { get; set; } = 6;

  /// <summary>The longest delay between those retries. Defaults to 30 seconds.</summary>
  public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

  /// <summary>
  /// Whether to claim sagas with a locking query (<c>UPDLOCK, READPAST</c>), so instances never claim the same saga
  /// and skip rows another instance is claiming. Defaults to <see langword="true"/>; set it to
  /// <see langword="false"/> to use the provider-neutral claim.
  /// </summary>
  public bool UseLockingClaim { get; set; } = true;

  /// <summary>Whether the worker applies pending migrations before it starts. Use it in development only.</summary>
  public bool ApplyMigrationsOnStartup { get; set; }
}
