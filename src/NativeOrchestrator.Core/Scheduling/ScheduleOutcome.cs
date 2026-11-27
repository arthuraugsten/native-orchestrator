namespace NativeOrchestrator;

/// <summary>What happened to one occurrence of a schedule. Used in logs and the <c>sagaorchestrator.schedule.occurrences</c> metric.</summary>
internal enum ScheduleOutcome
{
  /// <summary>A saga was started.</summary>
  Started,

  /// <summary>Another instance had already started this occurrence.</summary>
  Duplicate,

  /// <summary>The saga is single-instance and one was still active, so the occurrence was skipped.</summary>
  Skipped,

  /// <summary>The occurrence was too late (past the misfire threshold) or superseded by a later one, so it didn't start.</summary>
  Misfired,

  /// <summary>Starting the saga failed.</summary>
  Failed,
}
