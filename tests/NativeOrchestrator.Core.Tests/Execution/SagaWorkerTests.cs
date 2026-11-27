using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NativeOrchestrator.Tests;

public sealed class SagaWorkerTests
{
  [Fact]
  public async Task The_worker_claims_and_runs_due_sagas_until_they_finish()
  {
    var script = new Script();
    script.Check["Wait"] = context => context.CheckCount >= 2 ? PollResult.Succeeded() : PollResult.Pending();
    await using var provider = CreateServices(script, workerEnabled: true);
    var client = provider.GetRequiredService<ISagaClient>();
    var worker = provider.GetServices<IHostedService>().Single();

    await worker.StartAsync(TestContext.Current.CancellationToken);
    try
    {
      var ids = new List<Guid>();
      for (var i = 0; i < 10; i++)
        ids.Add((await client.StartAsync(SagaTestHost.SagaType, new TestData(), cancellationToken: TestContext.Current.CancellationToken)).SagaId);

      var deadline = DateTime.UtcNow.AddSeconds(15);
      foreach (var id in ids)
      {
        while ((await client.GetAsync(id, TestContext.Current.CancellationToken))?.Status != SagaStatus.Completed)
        {
          Assert.True(DateTime.UtcNow < deadline, $"Saga {id} didn't complete in time.");
          await Task.Delay(20, TestContext.Current.CancellationToken);
        }
      }
    }
    finally
    {
      await worker.StopAsync(CancellationToken.None);
    }

    Assert.Equal(10, script.Journal.Count(e => e == "start:Wait"));
  }

  [Fact]
  public async Task A_disabled_worker_runs_nothing()
  {
    var script = new Script();
    await using var provider = CreateServices(script, workerEnabled: false);
    var client = provider.GetRequiredService<ISagaClient>();
    var worker = provider.GetServices<IHostedService>().Single();

    await worker.StartAsync(TestContext.Current.CancellationToken);
    var id = (await client.StartAsync(SagaTestHost.SagaType, new TestData(), cancellationToken: TestContext.Current.CancellationToken)).SagaId;
    await Task.Delay(200, TestContext.Current.CancellationToken);
    await worker.StopAsync(CancellationToken.None);

    Assert.Equal(SagaStatus.Pending, (await client.GetAsync(id, TestContext.Current.CancellationToken))?.Status);
    Assert.Empty(script.Journal);
  }

  private static ServiceProvider CreateServices(Script script, bool workerEnabled)
  {
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton(new SagaShape().Step("A").PollingStep("Wait"));
    services.AddSingleton(script);
    services.AddNativeOrchestrator(options =>
      {
        options.WorkerEnabled = workerEnabled;
        options.ScanInterval = TimeSpan.FromMilliseconds(50);
        options.MaxConcurrency = 4;
      })
      .UseInMemoryStore()
      .AddSaga<ScriptedSaga, TestData>(SagaTestHost.SagaType, options =>
        options.Polling = new PollingOptions { InitialDelay = TimeSpan.FromMilliseconds(20), Multiplier = 1, MaxDelay = TimeSpan.FromMilliseconds(20) });
    return services.BuildServiceProvider();
  }
}
