namespace NativeOrchestrator;

/// <summary>
/// The default <see cref="IBackoffStrategy"/>: <c>InitialDelay × Multiplier^(attempt − 1)</c>, capped at
/// <c>MaxDelay</c>. With jitter, the delay is picked at random between half and all of that value.
/// </summary>
public sealed class ExponentialBackoffStrategy : IBackoffStrategy
{
  /// <inheritdoc />
  public TimeSpan GetDelay(int attempt, BackoffOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);
    ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);

    var seconds = options.InitialDelay.TotalSeconds * Math.Pow(options.Multiplier, attempt - 1);
    var delay = double.IsFinite(seconds) && seconds < options.MaxDelay.TotalSeconds
      ? TimeSpan.FromSeconds(seconds)
      : options.MaxDelay;

    if (!options.UseJitter)
      return delay;
    var half = delay / 2;
    return half + half * Random.Shared.NextDouble();
  }
}
