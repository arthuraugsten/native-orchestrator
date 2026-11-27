using DotNet.Testcontainers.Builders;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NativeOrchestrator.Data.SqlServer;
using NativeOrchestrator.Tests;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(SqlServerFixture))]

namespace NativeOrchestrator.Tests;

/// <summary>
/// One SQL Server container for the test run, started with Testcontainers. Each test gets its own database with the
/// migrations applied. If Docker isn't available, the tests are skipped with the reason.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
  private const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

  private MsSqlContainer? _container;
  private string? _skipReason;

  public async ValueTask InitializeAsync()
  {
    try
    {
      _container = new MsSqlBuilder(Image).Build();
      await _container.StartAsync();
    }
    catch (DockerUnavailableException ex)
    {
      _skipReason = $"Docker isn't available, so the SQL Server tests can't run: {ex.Message}";
    }
  }

  /// <summary>Creates an empty database with the migrations applied, and returns its connection string.</summary>
  public async Task<string> CreateDatabaseAsync()
  {
    Assert.SkipWhen(_skipReason is not null, _skipReason ?? "");
    var connectionString = new SqlConnectionStringBuilder(_container!.GetConnectionString())
    {
      InitialCatalog = $"saga_{Guid.NewGuid():N}",
    }.ConnectionString;

    await using var context = CreateContext(connectionString);
    await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    return connectionString;
  }

  internal static SagaDbContext CreateContext(string connectionString)
  {
    var builder = new DbContextOptionsBuilder<SagaDbContext>();
    SagaSqlServerDbContextOptions.Configure(builder, new SqlServerSagaStoreOptions { ConnectionString = connectionString });
    return new SagaDbContext(builder.Options);
  }

  public async ValueTask DisposeAsync()
  {
    if (_container is not null)
      await _container.DisposeAsync();
  }
}
