namespace NativeOrchestrator.Tests;

public sealed class BackoffTests
{
  private readonly ExponentialBackoffStrategy _backoff = new();

  [Fact]
  public void The_delay_grows_by_the_multiplier_and_is_capped()
  {
    var options = new RetryOptions { InitialDelay = TimeSpan.FromSeconds(10), Multiplier = 3, MaxDelay = TimeSpan.FromMinutes(1), UseJitter = false };

    Assert.Equal(
      [10, 30, 60, 60],
      Enumerable.Range(1, 4).Select(attempt => _backoff.GetDelay(attempt, options).TotalSeconds));
    Assert.Equal(TimeSpan.FromMinutes(1), _backoff.GetDelay(10_000, options));
  }

  [Fact]
  public void Jitter_keeps_the_delay_between_half_and_all_of_its_value()
  {
    var options = new RetryOptions { InitialDelay = TimeSpan.FromSeconds(10), Multiplier = 1, UseJitter = true };

    for (var i = 0; i < 1000; i++)
    {
      var delay = _backoff.GetDelay(1, options);
      Assert.InRange(delay, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
    }
  }

  [Fact]
  public void The_default_policy_retries_until_the_maximum_attempts()
  {
    var policy = new DefaultRetryPolicy(_backoff);
    var options = new RetryOptions { MaxAttempts = 2, InitialDelay = TimeSpan.FromSeconds(1), UseJitter = false };
    var error = new InvalidOperationException();

    Assert.Equal(RetryDecision.After(TimeSpan.FromSeconds(1)), policy.Evaluate(new RetryContext("saga", "step", 1, error, options, false)));
    Assert.Equal(RetryDecision.Stop, policy.Evaluate(new RetryContext("saga", "step", 2, error, options, false)));
  }
}
