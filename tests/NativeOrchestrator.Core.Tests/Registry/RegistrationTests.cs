using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NativeOrchestrator.Tests;

public sealed class RegistrationTests
{
  [Fact]
  public async Task A_saga_with_no_steps_fails_when_the_registry_is_built()
  {
    await using var host = SagaTestHost.Create(new SagaShape());

    var error = Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<SagaRegistry>());
    Assert.Contains("has no steps", error.Message);
  }

  [Fact]
  public async Task Two_steps_with_the_same_name_fail_when_the_registry_is_built()
  {
    await using var host = SagaTestHost.Create(new SagaShape().Step("A").Step("a"));

    var error = Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<SagaRegistry>());
    Assert.Contains("two steps named 'a'", error.Message);
  }

  [Fact]
  public async Task Invalid_step_options_in_code_fail_when_the_registry_is_built()
  {
    await using var host = SagaTestHost.Create(new SagaShape().Step("A", o => o.Timeout = TimeSpan.Zero));

    var error = Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<SagaRegistry>());
    Assert.Contains("Timeout must be positive", error.Message);
  }

  [Fact]
  public void Registering_two_sagas_with_the_same_name_throws()
  {
    var builder = new ServiceCollection().AddSagaOrchestrator();
    builder.AddSaga<ScriptedSaga, TestData>("orders");

    Assert.Throws<InvalidOperationException>(() => builder.AddSaga<ScriptedSaga, TestData>("orders"));
  }

  [Fact]
  public async Task Invalid_saga_options_fail_validation()
  {
    await using var host = SagaTestHost.Create(
      new SagaShape().Step("A"),
      configuration: new Dictionary<string, string?>
      {
        ["Sagas:scripted:Retry:MaxAttempts"] = "0",
        ["Sagas:scripted:Steps:A:Polling:Multiplier"] = "0.5",
      });

    var error = Assert.Throws<OptionsValidationException>(
      () => host.Services.GetRequiredService<IOptionsMonitor<SagaOptions>>().Get(SagaTestHost.SagaType));
    Assert.Contains(error.Failures, f => f.Contains("Retry:MaxAttempts", StringComparison.Ordinal));
    Assert.Contains(error.Failures, f => f.Contains("Steps:A:Polling:Multiplier", StringComparison.Ordinal));
  }

  [Fact]
  public async Task Invalid_orchestrator_options_fail_validation()
  {
    await using var host = SagaTestHost.Create(
      new SagaShape().Step("A"),
      configuration: new Dictionary<string, string?> { ["LeaseRenewalInterval"] = "00:10:00", ["LeaseDuration"] = "00:05:00" });

    var error = Assert.Throws<OptionsValidationException>(() => host.Services.GetRequiredService<IOptions<SagaOrchestratorOptions>>().Value);
    Assert.Contains(error.Failures, f => f.Contains("LeaseRenewalInterval", StringComparison.Ordinal));
  }

  [Fact]
  public void Default_step_and_saga_names_drop_their_suffix()
  {
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton(new Script());
    services.AddSagaOrchestrator().UseInMemoryStore().AddSaga<OrderFulfillmentSaga, TestData>();
    using var provider = services.BuildServiceProvider();

    var runtime = provider.GetRequiredService<SagaRegistry>().Get("OrderFulfillment");
    Assert.Equal("Scripted", Assert.Single(runtime.Steps).Name);
  }

  private sealed class OrderFulfillmentSaga : Saga<TestData>
  {
    protected override void Configure(ISagaBuilder<TestData> saga) => saga.Step<ScriptedStep>();
  }
}
