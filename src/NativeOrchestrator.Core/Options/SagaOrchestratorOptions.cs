namespace NativeOrchestrator;

/// <summary>Options for the orchestrator as a whole: the worker that claims and runs due sagas.</summary>
public sealed class SagaOrchestratorOptions
{
  /// <summary>Whether this instance runs the worker. Turn it off for an instance that only starts and reads sagas.</summary>
  public bool WorkerEnabled { get; set; } = true;

  /// <summary>The name this instance claims sagas under. Defaults to the machine name and process ID.</summary>
  public string InstanceName { get; set; } = $"{Environment.MachineName}:{Environment.ProcessId}";

  /// <summary>The most sagas this instance advances at once.</summary>
  public int MaxConcurrency { get; set; } = 16;

  /// <summary>How long the worker waits before looking for due sagas again, when none were due.</summary>
  public TimeSpan ScanInterval { get; set; } = TimeSpan.FromSeconds(5);

  /// <summary>How long a claimed saga stays locked to this instance. If the instance stops, another can claim it after this.</summary>
  public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);

  /// <summary>How often the lease is renewed while a step runs. Must be shorter than <see cref="LeaseDuration"/>.</summary>
  public TimeSpan LeaseRenewalInterval { get; set; } = TimeSpan.FromMinutes(1);

  /// <summary>How long to put off a saga whose type isn't registered (for example, during a rolling deployment).</summary>
  public TimeSpan UnknownSagaTypeRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

  /// <summary>The longest error message stored for a step or saga; longer messages are truncated.</summary>
  public int MaxErrorLength { get; set; } = 4000;
}
