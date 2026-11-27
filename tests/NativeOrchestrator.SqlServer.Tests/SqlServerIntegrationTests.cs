using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NativeOrchestrator.EntityFrameworkCore;
using NativeOrchestrator.SqlServer;

namespace NativeOrchestrator.Tests;

/// <summary>The engine, the claim queries and the schema against a real SQL Server.</summary>
public sealed class SqlServerIntegrationTests
{
  private readonly SqlServerFixture _fixture;

  public SqlServerIntegrationTests(SqlServerFixture fixture)
  {
    _fixture = fixture;
  }

  [Fact]
  public async Task A_saga_polls_for_more_than_12_hours_then_completes_with_every_step_and_history_stored()
  {
    var connectionString = await _fixture.CreateDatabaseAsync();
    await using var host = SagaTestHost.Create(
      new SagaShape()
        .Step("Request")
        .PollingStep("Export", o =>
        {
          o.Timeout = TimeSpan.FromHours(48);
          o.Polling = new PollingOptions { InitialDelay = TimeSpan.FromMinutes(30), Multiplier = 2, MaxDelay = TimeSpan.FromHours(2) };
        })
        .Step("Publish"),
      configureSaga: o => o.MaxDuration = TimeSpan.FromDays(3),
      useStore: builder => builder.UseSqlServer(o => o.ConnectionString = connectionString));
    host.Script.Check["Export"] = context =>
      context.TimeProvider.GetUtcNow() - context.StepStartedAtUtc >= TimeSpan.FromHours(14) ? PollResult.Succeeded() : PollResult.Pending();

    var id = await host.StartAsync(correlationId: "report-2026-01");
    var saga = await host.RunUntilFinishedAsync(id);

    Assert.Equal(SagaStatus.Completed, saga.Status);
    Assert.True(saga.CompletedAtUtc - saga.StartedAtUtc >= TimeSpan.FromHours(14));
    Assert.Equal(["Request", "Export", "Publish"], saga.Steps.Select(s => s.Name));
    Assert.Equal(["Request", "Export", "Publish"], saga.Data.GetProperty("trace").EnumerateArray().Select(e => e.GetString()));
    var history = await host.Client.GetHistoryAsync(id);
    Assert.Equal(SagaEventType.Created, history[0].EventType);
    Assert.Equal(SagaEventType.Completed, history[^1].EventType);

    await using var context = SqlServerFixture.CreateContext(connectionString);
    var row = await context.Sagas.AsNoTracking().SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
    Assert.Null(row.NextRunAtUtc);
    Assert.Null(await context.Sagas.Where(s => s.Id == id).Select(s => EF.Property<Guid?>(s, SagaModel.LeaseToken)).SingleAsync(TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task A_failure_compensates_the_succeeded_steps_in_reverse_order()
  {
    var connectionString = await _fixture.CreateDatabaseAsync();
    await using var host = SagaTestHost.Create(
      new SagaShape().Step("Reserve").Step("Charge").Step("Ship"),
      useStore: builder => builder.UseSqlServer(o => o.ConnectionString = connectionString));
    host.Script.Execute["Ship"] = _ => StepResult.Failure("carrier rejected the parcel");

    var saga = await host.RunUntilFinishedAsync(await host.StartAsync());

    Assert.Equal(SagaStatus.Compensated, saga.Status);
    Assert.Equal(["undo:Charge@1", "undo:Reserve@1"], host.Script.Journal.Where(e => e.StartsWith("undo:", StringComparison.Ordinal)));
    Assert.Equal([StepStatus.Compensated, StepStatus.Compensated, StepStatus.Failed], saga.Steps.Select(s => s.Status));
  }

  [Fact]
  public async Task A_cancellation_while_a_step_runs_is_detected_by_the_version_check()
  {
    var connectionString = await _fixture.CreateDatabaseAsync();
    await using var host = SagaTestHost.Create(
      new SagaShape().Step("A").Step("B"),
      useStore: builder => builder.UseSqlServer(o => o.ConnectionString = connectionString));
    var id = await host.StartAsync();
    host.Script.Execute["B"] = context =>
    {
      host.Client.CancelAsync(context.SagaId).GetAwaiter().GetResult();
      return StepResult.Success();
    };

    var saga = await host.RunUntilFinishedAsync(id);

    Assert.Equal(SagaStatus.Compensated, saga.Status);
    Assert.Equal(SagaFailureKind.Cancelled, saga.Failure?.Kind);
    Assert.Equal(["A@1", "B@1", "undo:A@1"], host.Script.Journal);
  }

  [Fact]
  public async Task Concurrent_claimers_with_the_locking_claim_never_take_the_same_saga()
  {
    var connectionString = await _fixture.CreateDatabaseAsync();
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddNativeOrchestrator().UseSqlServer(o => o.ConnectionString = connectionString);
    await using var provider = services.BuildServiceProvider();
    var repository = provider.GetRequiredService<ISagaRepository>();
    var queue = provider.GetRequiredService<ISagaWorkQueue>();
    var now = DateTimeOffset.UtcNow;
    for (var i = 0; i < 60; i++)
    {
      var saga = new SagaRecord
      {
        Id = Guid.CreateVersion7(),
        SagaType = "contended",
        DataJson = "{}",
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
        NextRunAtUtc = now.AddSeconds(-i),
      };
      await repository.CreateAsync(saga, new SagaHistoryEntry(saga.Id, saga.SagaType, now, SagaEventType.Created, null, null), TestContext.Current.CancellationToken);
    }

    var claims = new ConcurrentBag<Guid>();
    await Parallel.ForAsync(0, 6, TestContext.Current.CancellationToken, async (worker, token) =>
    {
      while (true)
      {
        var claimed = await queue.ClaimDueAsync(new SagaClaimRequest($"worker-{worker}", 3, now, now.AddMinutes(5)), token);
        if (claimed.Count == 0)
          return;
        foreach (var saga in claimed)
          claims.Add(saga.Saga.Id);
      }
    });

    Assert.Equal(60, claims.Count);
    Assert.Equal(60, claims.Distinct().Count());
  }

  [Fact]
  public async Task Instances_that_fire_the_same_occurrence_at_once_start_one_saga()
  {
    var connectionString = await _fixture.CreateDatabaseAsync();
    var hosts = new List<SagaTestHost>();
    try
    {
      for (var i = 0; i < 4; i++)
      {
        var host = SagaTestHost.Create(
          new SagaShape().Step("A"),
          useStore: builder => builder.UseSqlServer(o => o.ConnectionString = connectionString),
          configureOrchestrator: builder => builder.AddSchedule("hourly", SagaTestHost.SagaType, "0 * * * *", _ => new TestData()));
        host.Time.SetUtcNow(SagaTestHost.Start.AddHours(1));
        hosts.Add(host);
      }

      await Task.WhenAll(hosts.Select(h => h.RunSchedulesAsync()));
    }
    finally
    {
      foreach (var host in hosts)
        await host.DisposeAsync();
    }

    await using var context = SqlServerFixture.CreateContext(connectionString);
    var correlationIds = await context.Sagas.AsNoTracking().Select(s => s.CorrelationId).ToListAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["schedule:hourly:20260101T010000Z"], correlationIds);
  }

  [Fact]
  public async Task The_database_initializer_applies_migrations_when_asked_to()
  {
    var connectionString = (await _fixture.CreateDatabaseAsync()).Replace("saga_", "fresh_", StringComparison.Ordinal);
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddNativeOrchestrator().UseSqlServer(o =>
    {
      o.ConnectionString = connectionString;
      o.ApplyMigrationsOnStartup = true;
    });
    await using var provider = services.BuildServiceProvider();

    foreach (var initializer in provider.GetServices<ISagaStoreInitializer>())
      await initializer.InitializeAsync(TestContext.Current.CancellationToken);

    await using var context = SqlServerFixture.CreateContext(connectionString);
    Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    Assert.IsType<SqlServerClaimStrategy>(provider.GetRequiredService<ISagaClaimStrategy>());
  }
}
