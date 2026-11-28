namespace NativeOrchestrator;

/// <summary>How a delay grows between attempts: <c>InitialDelay × Multiplier^(attempt − 1)</c>, capped at <see cref="MaxDelay"/>.</summary>
public abstract class BackoffOptions
{
  /// <summary>The delay before the first retry or check.</summary>
  public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(10);

  /// <summary>The longest delay between attempts.</summary>
  public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(10);

  /// <summary>How much the delay grows after each attempt. 1 keeps it constant.</summary>
  public double Multiplier { get; set; } = 2;

  /// <summary>Whether to randomise each delay between half and all of its value, so many sagas don't retry in step.</summary>
  public bool UseJitter { get; set; }
}
