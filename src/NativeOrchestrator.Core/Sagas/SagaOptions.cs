namespace NativeOrchestrator;

/// <summary>
/// Options for one saga type, as named options (the name is the saga type name). They are bound from
/// <c>SagaDefaults</c> and then <c>Sagas:{name}</c> when the orchestrator is added with a configuration section.
/// </summary>
public sealed class SagaOptions
{
  /// <summary>
  /// Whether at most one saga of this type may be active at a time. A start while one is returns that saga, with
  /// <see cref="SagaStartOutcome.AlreadyRunning"/>, instead of creating another. A completed or compensated saga doesn't
  /// block; a <see cref="SagaStatus.Failed"/> one does unless <see cref="FailedBlocksNewStarts"/> is turned off. The
  /// store enforces it, so it holds across instances and concurrent starts. Defaults to <see langword="false"/>.
  /// </summary>
  public bool SingleInstance { get; set; }

  /// <summary>
  /// For a <see cref="SingleInstance"/> saga: whether one that ends <see cref="SagaStatus.Failed"/> (a compensation
  /// failed, so it needs manual action) keeps blocking new starts until an operator releases it. Turn it off to let the
  /// next saga start as soon as one fails. Read when the saga finishes. Defaults to <see langword="true"/>.
  /// </summary>
  public bool FailedBlocksNewStarts { get; set; } = true;

  /// <summary>How long a saga may run before it times out and compensates. Defaults to 7 days.</summary>
  public TimeSpan MaxDuration { get; set; } = TimeSpan.FromDays(7);

  /// <summary>How long a step may run in total, across retries and polls. Defaults to 24 hours.</summary>
  public TimeSpan StepTimeout { get; set; } = TimeSpan.FromHours(24);

  /// <summary>How long one call to a step (or a compensation) may take. Defaults to 2 minutes.</summary>
  public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromMinutes(2);

  /// <summary>Retries after transient failures.</summary>
  public RetryOptions Retry { get; set; } = new();

  /// <summary>How often polling steps check their operation.</summary>
  public PollingOptions Polling { get; set; } = new();

  /// <summary>Retries of compensations. Defaults to 10 attempts.</summary>
  public RetryOptions CompensationRetry { get; set; } = new() { MaxAttempts = 10 };

  /// <summary>Per-step overrides, by step name (case-insensitive).</summary>
  public Dictionary<string, StepOptions> Steps { get; } = [with(StringComparer.OrdinalIgnoreCase)];
}
