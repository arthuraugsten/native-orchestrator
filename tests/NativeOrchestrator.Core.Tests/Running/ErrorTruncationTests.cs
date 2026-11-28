namespace NativeOrchestrator.Tests;

public sealed class ErrorTruncationTests
{
  [Fact]
  public async Task A_long_step_error_is_truncated_in_the_step_the_saga_and_every_history_entry()
  {
    await using var host = SagaTestHost.Create(
      new SagaShape().Step("A").Step("B"),
      configuration: new Dictionary<string, string?> { ["MaxErrorLength"] = "100" });
    host.Script.Execute["B"] = _ => StepResult.Failure(new string('x', 500));

    var id = await host.StartAsync();
    var saga = await host.RunUntilFinishedAsync(id);
    var history = await host.Client.GetHistoryAsync(id, TestContext.Current.CancellationToken);

    Assert.Equal(SagaStatus.Compensated, saga.Status);
    Assert.Equal(100, saga.Failure!.Error.Length);
    Assert.EndsWith("...", saga.Failure.Error, StringComparison.Ordinal);
    Assert.All(saga.Steps, step => Assert.True((step.Error?.Length ?? 0) <= 100));
    // CompensationStarted prefixes the failure kind to an already truncated error; it is still within the limit.
    Assert.Contains(history, h => h.EventType == SagaEventType.CompensationStarted);
    Assert.All(history, h => Assert.True((h.Details?.Length ?? 0) <= 100, $"{h.EventType}: {h.Details?.Length}"));
  }
}
