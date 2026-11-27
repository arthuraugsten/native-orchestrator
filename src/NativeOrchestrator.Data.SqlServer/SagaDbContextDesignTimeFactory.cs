using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NativeOrchestrator.Data.SqlServer;

/// <summary>
/// Lets <c>dotnet ef</c> create the context to add migrations:
/// <c>dotnet ef migrations add &lt;Name&gt; --project src/NativeOrchestrator.Data.SqlServer</c>. It never connects.
/// </summary>
internal sealed class SagaDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SagaDbContext>
{
  public SagaDbContext CreateDbContext(string[] args)
  {
    var builder = new DbContextOptionsBuilder<SagaDbContext>();
    SagaSqlServerDbContextOptions.Configure(builder, new SqlServerSagaStoreOptions
    {
      ConnectionString = "Server=localhost;Database=NativeOrchestrator;Integrated Security=true;TrustServerCertificate=true",
    });
    return new SagaDbContext(builder.Options);
  }
}
