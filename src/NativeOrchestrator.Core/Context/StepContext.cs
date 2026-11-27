using System.Diagnostics.CodeAnalysis;

namespace NativeOrchestrator;

/// <summary>What a step sees when it runs: the saga's data, its own saved state, and identifiers for idempotency.</summary>
/// <typeparam name="TData">The saga's data.</typeparam>
public sealed class StepContext<TData>
  where TData : class
{
  private readonly SagaRecord _saga;
  private readonly StepRecord _step;
  private readonly ISagaSerializer _serializer;

  internal StepContext(
    SagaRecord saga,
    StepRecord step,
    TData data,
    bool isCompensation,
    ISagaSerializer serializer,
    IServiceProvider services,
    TimeProvider timeProvider)
  {
    _saga = saga;
    _step = step;
    _serializer = serializer;
    Data = data;
    IsCompensation = isCompensation;
    Services = services;
    TimeProvider = timeProvider;
    StateJson = step.StateJson;
  }

  /// <summary>The saga's identifier.</summary>
  public Guid SagaId => _saga.Id;

  /// <summary>The saga type name.</summary>
  public string SagaType => _saga.SagaType;

  /// <summary>The caller's correlation identifier, if one was given.</summary>
  public string? CorrelationId => _saga.CorrelationId;

  /// <summary>The step's name.</summary>
  public string StepName => _step.Name;

  /// <summary>Whether this is a call to <see cref="SagaStep{TData}.CompensateAsync"/>.</summary>
  public bool IsCompensation { get; }

  /// <summary>Which attempt this is, from 1. It goes up after each transient failure and resets after a poll.</summary>
  public int Attempt => (IsCompensation ? _step.CompensationAttempt : _step.Attempt) + 1;

  /// <summary>For a polling step, how many times the operation has been checked before this run.</summary>
  public int CheckCount => _step.CheckCount;

  /// <summary>When the step first started.</summary>
  public DateTimeOffset StepStartedAtUtc => _step.StartedAtUtc;

  /// <summary>
  /// A key that is the same every time this step runs for this saga (and different for its compensation). Send it to
  /// external systems so they can ignore repeated requests.
  /// </summary>
  public string IdempotencyKey => IsCompensation ? $"{SagaId:N}:{StepName}:compensate" : $"{SagaId:N}:{StepName}";

  /// <summary>The saga's data. Changes are saved when the step returns <see cref="StepResult.Success"/> or <see cref="StepResult.Poll"/>.</summary>
  public TData Data { get; }

  /// <summary>The DI scope the step runs in.</summary>
  public IServiceProvider Services { get; }

  /// <summary>The clock. Use it instead of <see cref="DateTimeOffset.UtcNow"/>, so tests can control time.</summary>
  public TimeProvider TimeProvider { get; }

  internal string? StateJson { get; private set; }

  /// <summary>Reads the state the step saved on an earlier run, if any.</summary>
  /// <typeparam name="TState">The state's type.</typeparam>
  /// <param name="state">The state.</param>
  public bool TryGetState<TState>([MaybeNullWhen(false)] out TState state)
  {
    if (StateJson is null)
    {
      state = default;
      return false;
    }

    state = _serializer.Deserialize<TState>(StateJson);
    return true;
  }

  /// <summary>Saves state for the step's later runs. It is kept when the step returns <see cref="StepResult.Success"/> or <see cref="StepResult.Poll"/>.</summary>
  /// <typeparam name="TState">The state's type.</typeparam>
  /// <param name="state">The state.</param>
  public void SetState<TState>(TState state)
  {
    ArgumentNullException.ThrowIfNull(state);
    StateJson = _serializer.Serialize(state);
  }

  /// <summary>Removes the step's saved state.</summary>
  public void ClearState() => StateJson = null;
}
