namespace NativeOrchestrator;

/// <summary>What running a step (or a compensation) left the saga needing.</summary>
internal enum RunOutcome
{
  /// <summary>Something finished; save, then carry on in the same run.</summary>
  Continue,

  /// <summary>The saga waits for a later time (a poll or a retry); save and end the run.</summary>
  Wait,

  /// <summary>Nothing is left to compensate.</summary>
  Done,
}
