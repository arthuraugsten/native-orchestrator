namespace NativeOrchestrator.Tests;

/// <summary>Saga data for tests: steps append to <see cref="Trace"/> when they succeed.</summary>
public sealed class TestData
{
  public List<string> Trace { get; set; } = [];

  public string? Reject { get; set; }
}

/// <summary>Declares the shape of <see cref="ScriptedSaga"/>: its steps, in order.</summary>
public sealed class SagaShape
{
  public List<(string Name, bool Polling, Action<StepOptions>? Configure)> Steps { get; } = [];

  public SagaShape Step(string name, Action<StepOptions>? configure = null)
  {
    Steps.Add((name, false, configure));
    return this;
  }

  public SagaShape PollingStep(string name, Action<StepOptions>? configure = null)
  {
    Steps.Add((name, true, configure));
    return this;
  }
}

/// <summary>What each scripted step does, and a journal of what happened, with times.</summary>
public sealed class Script
{
  private readonly Lock _lock = new();
  private readonly List<string> _journal = [];
  private readonly List<(string Entry, DateTimeOffset At)> _timeline = [];

  public Dictionary<string, Func<StepContext<TestData>, StepResult>> Execute { get; } = [];

  public Dictionary<string, Action<StepContext<TestData>>> Compensate { get; } = [];

  /// <summary>For polling steps: decides each check's result.</summary>
  public Dictionary<string, Func<StepContext<TestData>, PollResult>> Check { get; } = [];

  public List<string> Finished { get; } = [];

  public IReadOnlyList<string> Journal
  {
    get
    {
      lock (_lock)
        return [.. _journal];
    }
  }

  public IReadOnlyList<(string Entry, DateTimeOffset At)> Timeline
  {
    get
    {
      lock (_lock)
        return [.. _timeline];
    }
  }

  public void Write(string entry, DateTimeOffset at)
  {
    lock (_lock)
    {
      _journal.Add(entry);
      _timeline.Add((entry, at));
    }
  }
}

public sealed class ScriptedSaga : Saga<TestData>
{
  private readonly SagaShape _shape;
  private readonly Script _script;

  public ScriptedSaga(SagaShape shape, Script script)
  {
    _shape = shape;
    _script = script;
  }

  protected override void Configure(ISagaBuilder<TestData> saga)
  {
    foreach (var (name, polling, configure) in _shape.Steps)
    {
      if (polling)
        saga.Step<ScriptedPollingStep>(name, configure);
      else
        saga.Step<ScriptedStep>(name, configure);
    }
  }

  protected override string? Validate(TestData data) => data.Reject;

  protected override ValueTask OnCompletedAsync(SagaContext<TestData> context, CancellationToken cancellationToken)
  {
    _script.Finished.Add($"completed:{context.Status}");
    return ValueTask.CompletedTask;
  }

  protected override ValueTask OnFailedAsync(SagaContext<TestData> context, SagaFailure failure, CancellationToken cancellationToken)
  {
    _script.Finished.Add($"failed:{context.Status}:{failure.Kind}");
    return ValueTask.CompletedTask;
  }
}

public sealed class ScriptedStep : SagaStep<TestData>
{
  private readonly Script _script;

  public ScriptedStep(Script script)
  {
    _script = script;
  }

  public override Task<StepResult> ExecuteAsync(StepContext<TestData> context, CancellationToken cancellationToken)
  {
    _script.Write($"{context.StepName}@{context.Attempt}", context.TimeProvider.GetUtcNow());
    context.Data.Trace.Add(context.StepName);
    var result = _script.Execute.TryGetValue(context.StepName, out var behaviour) ? behaviour(context) : StepResult.Success();
    return Task.FromResult(result);
  }

  public override Task CompensateAsync(StepContext<TestData> context, CancellationToken cancellationToken)
  {
    _script.Write($"undo:{context.StepName}@{context.Attempt}", context.TimeProvider.GetUtcNow());
    if (_script.Compensate.TryGetValue(context.StepName, out var behaviour))
      behaviour(context);
    return Task.CompletedTask;
  }
}

public sealed class ScriptedPollingStep : PollingSagaStep<TestData, string>
{
  private readonly Script _script;

  public ScriptedPollingStep(Script script)
  {
    _script = script;
  }

  protected override Task<string> StartAsync(StepContext<TestData> context, CancellationToken cancellationToken)
  {
    _script.Write($"start:{context.StepName}", context.TimeProvider.GetUtcNow());
    return Task.FromResult($"operation-{context.SagaId:N}");
  }

  protected override Task<PollResult> CheckAsync(StepContext<TestData> context, string operation, CancellationToken cancellationToken)
  {
    _script.Write($"check:{context.StepName}", context.TimeProvider.GetUtcNow());
    var result = _script.Check.TryGetValue(context.StepName, out var behaviour) ? behaviour(context) : PollResult.Pending();
    if (result.State == PollState.Succeeded)
      context.Data.Trace.Add(context.StepName);
    return Task.FromResult(result);
  }

  public override Task CompensateAsync(StepContext<TestData> context, CancellationToken cancellationToken)
  {
    _script.Write($"undo:{context.StepName}@{context.Attempt}", context.TimeProvider.GetUtcNow());
    if (_script.Compensate.TryGetValue(context.StepName, out var behaviour))
      behaviour(context);
    return Task.CompletedTask;
  }
}
