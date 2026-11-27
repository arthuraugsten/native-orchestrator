namespace NativeOrchestrator;

/// <summary>
/// How often a polling step checks its operation. The polling step stops at its <see cref="StepOptions.Timeout"/>, so
/// there is no limit on the number of checks.
/// </summary>
public sealed class PollingOptions : BackoffOptions
{
  /// <summary>Creates polling options with the defaults: from 30 seconds, growing 1.5 times per check, to 10 minutes.</summary>
  public PollingOptions()
  {
    InitialDelay = TimeSpan.FromSeconds(30);
    Multiplier = 1.5;
  }
}
