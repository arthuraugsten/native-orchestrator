namespace NativeOrchestrator.App.ApiManagement;

/// <summary>How the simulated <see cref="ApiManagementService"/> behaves (section <c>ApiManagement</c>).</summary>
public sealed class ApiManagementOptions
{
  /// <summary>The longest an operation takes. Each one picks a random delay up to this when it starts. Real ones take hours.</summary>
  public TimeSpan MaxDuration { get; set; } = TimeSpan.FromSeconds(20);
}
