namespace NativeOrchestrator.App.Simulated;

/// <summary>How the simulated external systems behave (section <c>Simulated</c>).</summary>
public sealed class SimulatedOptions
{
  /// <summary>Whether to map the simulated endpoints under <c>/simulated</c>. Turn it off once real systems are configured.</summary>
  public bool Enabled { get; set; } = true;

  /// <summary>How long an identity verification takes.</summary>
  public TimeSpan IdentityCheckDuration { get; set; } = TimeSpan.FromSeconds(10);

  /// <summary>The <c>Retry-After</c> the simulated systems send while an operation is pending.</summary>
  public TimeSpan RetryAfter { get; set; } = TimeSpan.FromSeconds(2);
}
