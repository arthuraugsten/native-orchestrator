using Microsoft.Extensions.DependencyInjection;

namespace NativeOrchestrator.Tests;

/// <summary>The store contract on SQL Server, with the locking claim query (the default).</summary>
public sealed class SqlServerLockingClaimContractTests : SqlServerSagaStoreContractTests
{
  public SqlServerLockingClaimContractTests(SqlServerFixture fixture)
    : base(fixture, useLockingClaim: true)
  {
  }
}

/// <summary>The store contract on SQL Server, with the provider-neutral claim.</summary>
public sealed class SqlServerDefaultClaimContractTests : SqlServerSagaStoreContractTests
{
  public SqlServerDefaultClaimContractTests(SqlServerFixture fixture)
    : base(fixture, useLockingClaim: false)
  {
  }
}

public abstract class SqlServerSagaStoreContractTests : SagaStoreContractTests, IAsyncDisposable
{
  private readonly SqlServerFixture _fixture;
  private readonly bool _useLockingClaim;
  private ServiceProvider? _services;

  protected SqlServerSagaStoreContractTests(SqlServerFixture fixture, bool useLockingClaim)
  {
    _fixture = fixture;
    _useLockingClaim = useLockingClaim;
  }

  protected override async Task<(ISagaRepository Repository, ISagaWorkQueue Queue)> CreateStoreAsync()
  {
    var connectionString = await _fixture.CreateDatabaseAsync();
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddNativeOrchestrator().UseSqlServer(options =>
    {
      options.ConnectionString = connectionString;
      options.UseLockingClaim = _useLockingClaim;
    });
    _services = services.BuildServiceProvider();
    return (_services.GetRequiredService<ISagaRepository>(), _services.GetRequiredService<ISagaWorkQueue>());
  }

  public async ValueTask DisposeAsync()
  {
    if (_services is not null)
      await _services.DisposeAsync();
    GC.SuppressFinalize(this);
  }
}
