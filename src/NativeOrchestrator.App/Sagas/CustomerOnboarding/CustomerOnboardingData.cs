namespace NativeOrchestrator.App.Sagas.CustomerOnboarding;

/// <summary>The onboarding saga's data: the input, plus what the steps learn on the way.</summary>
public sealed class CustomerOnboardingData
{
  public string CustomerName { get; set; } = "";

  public string Email { get; set; } = "";

  /// <summary>Set by <see cref="ReserveAccountStep"/>.</summary>
  public string? AccountId { get; set; }

  /// <summary>Set by <see cref="VerifyIdentityStep"/>.</summary>
  public string? VerificationId { get; set; }
}
