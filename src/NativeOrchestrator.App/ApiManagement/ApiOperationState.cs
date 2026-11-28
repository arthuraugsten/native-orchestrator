namespace NativeOrchestrator.App.ApiManagement;

/// <summary>Where a long-running operation is.</summary>
public enum ApiOperationState
{
  /// <summary>Still running: check again later.</summary>
  Running,

  /// <summary>Finished successfully.</summary>
  Succeeded,

  /// <summary>Finished with an error. It won't succeed if checked again.</summary>
  Failed,
}
