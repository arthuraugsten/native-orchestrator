using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace NativeOrchestrator.Tests;

/// <summary>
/// The orchestrator with a fake clock and no worker: tests drive it run by run, jumping the clock to each saga's next
/// due time, so a saga that polls for days runs in milliseconds.
/// </summary>
public sealed class SagaTestHost : IAsyncDisposable
{
  public const string SagaType = "scripted";

  private readonly ServiceProvider _services;

  private SagaTestHost(ServiceProvider services, FakeTimeProvider time, Script script)
  {
    _services = services;
    Time = time;
    Script = script;
  }

  public static DateTimeOffset Start { get; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

  public FakeTimeProvider Time { get; }

  public Script Script { get; }

  public IServiceProvider Services => _services;

  public ISagaClient Client => _services.GetRequiredService<ISagaClient>();

  public ISagaWorkQueue Queue => _services.GetRequiredService<ISagaWorkQueue>();

  public int Runs { get; private set; }

  /// <summary>Creates a host with <see cref="ScriptedSaga"/> registered as <see cref="SagaType"/>.</summary>
  /// <param name="shape">The saga's steps.</param>
  /// <param name="configureSaga">Saga options in code.</param>
  /// <param name="configuration">Configuration values for <c>AddNativeOrchestrator(configuration)</c>.</param>
  /// <param name="useStore">Picks the store; the in-memory store by default.</param>
  /// <param name="configureServices">Adds or replaces services.</param>
  /// <param name="configureOrchestrator">Adds further sagas or customisations.</param>
  public static SagaTestHost Create(
    SagaShape shape,
    Action<SagaOptions>? configureSaga = null,
    IDictionary<string, string?>? configuration = null,
    Action<NativeOrchestratorBuilder>? useStore = null,
    Action<IServiceCollection>? configureServices = null,
    Action<NativeOrchestratorBuilder>? configureOrchestrator = null)
  {
    var time = new FakeTimeProvider(Start);
    var script = new Script();
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton<TimeProvider>(time);
    services.AddSingleton(shape);
    services.AddSingleton(script);

    var config = new ConfigurationBuilder().AddInMemoryCollection(configuration ?? new Dictionary<string, string?>()).Build();
    var builder = services.AddNativeOrchestrator(config);
    builder.AddSaga<ScriptedSaga, TestData>(SagaType, options =>
    {
      // Deterministic delays by default; tests that care set their own.
      options.Retry = new RetryOptions { InitialDelay = TimeSpan.FromSeconds(10), Multiplier = 2, MaxAttempts = 3, UseJitter = false };
      options.CompensationRetry = new RetryOptions { InitialDelay = TimeSpan.FromSeconds(10), Multiplier = 2, MaxAttempts = 3, UseJitter = false };
      options.Polling = new PollingOptions { InitialDelay = TimeSpan.FromMinutes(1), Multiplier = 1, MaxDelay = TimeSpan.FromMinutes(1) };
      configureSaga?.Invoke(options);
    });
    if (useStore is null)
      builder.UseInMemoryStore();
    else
      useStore(builder);
    configureOrchestrator?.Invoke(builder);
    configureServices?.Invoke(services);

    return new SagaTestHost(services.BuildServiceProvider(validateScopes: true), time, script);
  }

  public Task<Guid> StartAsync(TestData? data = null, string? correlationId = null) =>
    StartTypedAsync(data ?? new TestData(), correlationId);

  /// <summary>Claims and runs every saga that is due now.</summary>
  public async Task<int> RunDueAsync()
  {
    var engine = _services.GetRequiredService<SagaEngine>();
    var now = Time.GetUtcNow();
    var claimed = await Queue.ClaimDueAsync(new SagaClaimRequest("test", 100, now, now + TimeSpan.FromMinutes(5)), CancellationToken.None);
    foreach (var saga in claimed)
    {
      Runs++;
      await engine.RunAsync(saga, CancellationToken.None);
    }

    return claimed.Count;
  }

  /// <summary>Runs a saga until it finishes, moving the clock to each due time.</summary>
  public async Task<SagaSnapshot> RunUntilFinishedAsync(Guid sagaId, int maxRuns = 10_000)
  {
    for (var i = 0; i < maxRuns; i++)
    {
      var saga = await Client.GetAsync(sagaId) ?? throw new InvalidOperationException($"Saga {sagaId} doesn't exist.");
      if (saga.Status.IsFinished())
        return saga;
      if (saga.NextRunAtUtc > Time.GetUtcNow())
        Time.SetUtcNow(saga.NextRunAtUtc.Value);
      await RunDueAsync();
    }

    throw new InvalidOperationException($"Saga {sagaId} didn't finish within {maxRuns} runs.");
  }

  /// <summary>Runs a saga until it waits for a later time, or finishes.</summary>
  public async Task<SagaSnapshot> RunUntilWaitingAsync(Guid sagaId)
  {
    await RunDueAsync();
    return await Client.GetAsync(sagaId) ?? throw new InvalidOperationException($"Saga {sagaId} doesn't exist.");
  }

  /// <summary>Starts every schedule occurrence due now, as the scheduler does when it wakes; returns when it would wake next.</summary>
  public Task<DateTimeOffset> RunSchedulesAsync() =>
    _services.GetServices<IHostedService>().OfType<SagaScheduler>().Single().RunDueAsync(CancellationToken.None);

  public ValueTask DisposeAsync() => _services.DisposeAsync();

  private async Task<Guid> StartTypedAsync(TestData data, string? correlationId)
  {
    var result = await Client.StartAsync(SagaType, data, correlationId);
    return result.SagaId;
  }
}
