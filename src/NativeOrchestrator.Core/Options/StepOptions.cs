namespace NativeOrchestrator;

/// <summary>
/// Per-step overrides. A value left <see langword="null"/> falls back to the saga's <see cref="SagaOptions"/>. Set them
/// in code (<see cref="ISagaBuilder{TData}.Step{TStep}"/>) or in configuration (<c>Sagas:{saga}:Steps:{step}</c>);
/// configuration wins.
/// </summary>
public sealed class StepOptions
{
  /// <summary>How long the step may run in total, across retries and polls, before it fails. Defaults to <see cref="SagaOptions.StepTimeout"/>.</summary>
  public TimeSpan? Timeout { get; set; }

  /// <summary>How long one call to the step may take. Defaults to <see cref="SagaOptions.ExecutionTimeout"/>.</summary>
  public TimeSpan? ExecutionTimeout { get; set; }

  /// <summary>Whether the saga carries on when this step fails, instead of compensating. Defaults to <see langword="false"/>.</summary>
  public bool? ContinueOnError { get; set; }

  /// <summary>Retries after transient failures. Defaults to <see cref="SagaOptions.Retry"/>.</summary>
  public RetryOptions? Retry { get; set; }

  /// <summary>How often a polling step checks its operation. Defaults to <see cref="SagaOptions.Polling"/>.</summary>
  public PollingOptions? Polling { get; set; }

  /// <summary>Retries of the step's compensation. Defaults to <see cref="SagaOptions.CompensationRetry"/>.</summary>
  public RetryOptions? CompensationRetry { get; set; }
}
