namespace NativeOrchestrator.App.Sagas.Client;

/// <summary>The client saga's data.</summary>
public sealed class ClientSagaData
{
  /// <summary>Names the run in the external systems. Scheduled runs use <c>client-{yyyyMMdd-HHmm}</c>.</summary>
  public string RunName { get; set; } = "";

  /// <summary>When the schedule's occurrence was due, or <see langword="null"/> for a run started over HTTP.</summary>
  public DateTimeOffset? ScheduledAtUtc { get; set; }

  /// <summary>Set by <see cref="ProvisionClientStep"/>.</summary>
  public string? ProvisioningId { get; set; }

  /// <summary>Set by <see cref="SynchronizeClientStep"/>.</summary>
  public string? SynchronizationId { get; set; }
}
