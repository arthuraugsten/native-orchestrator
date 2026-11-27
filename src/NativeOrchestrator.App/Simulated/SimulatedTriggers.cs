namespace NativeOrchestrator.App.Simulated;

/// <summary>
/// Input values that make the simulated systems fail on purpose, so every saga outcome can be demonstrated. The
/// requests in <c>NativeOrchestrator.App.http</c> use them.
/// </summary>
public static class SimulatedTriggers
{
  /// <summary>A customer name containing this is rejected by identity verification (the onboarding saga compensates).</summary>
  public const string RejectedCustomer = "REJECT";

  /// <summary>A customer name containing this is never verified (the verification step times out; the saga compensates).</summary>
  public const string SlowCustomer = "SLOW";

  /// <summary>An email starting with this makes the accounts API answer 503 twice per request (the resilience handler retries).</summary>
  public const string FlakyEmailPrefix = "flaky";

  /// <summary>An email ending with this bounces (422). The welcome email has ContinueOnError; the report saga compensates.</summary>
  public const string BouncingEmailSuffix = "@bounce.test";

  /// <summary>A report name starting with this fails to generate.</summary>
  public const string FailingReportPrefix = "FAIL";
}
