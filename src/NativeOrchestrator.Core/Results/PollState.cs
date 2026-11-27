namespace NativeOrchestrator;

/// <summary>The state of a polled operation.</summary>
public enum PollState
{
  /// <summary>Still running.</summary>
  Pending,

  /// <summary>Finished successfully.</summary>
  Succeeded,

  /// <summary>Failed permanently.</summary>
  Failed,
}
