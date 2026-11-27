using Microsoft.Extensions.Logging;

namespace NativeOrchestrator;

/// <summary>Source-generated log messages, so logging doesn't allocate when the level is off.</summary>
internal static partial class SagaLog
{
  [LoggerMessage(1, LogLevel.Information, "Saga worker {InstanceName} started; it runs up to {MaxConcurrency} sagas at once")]
  public static partial void WorkerStarted(ILogger logger, string instanceName, int maxConcurrency);

  [LoggerMessage(2, LogLevel.Information, "Saga worker is disabled (NativeOrchestrator:WorkerEnabled is false)")]
  public static partial void WorkerDisabled(ILogger logger);

  [LoggerMessage(3, LogLevel.Error, "Claiming due sagas failed; trying again in {Delay}")]
  public static partial void ClaimFailed(ILogger logger, Exception exception, TimeSpan delay);

  [LoggerMessage(10, LogLevel.Information, "Saga {SagaId} ({SagaType}) finished: {Status}")]
  public static partial void SagaFinished(ILogger logger, Guid sagaId, string sagaType, SagaStatus status);

  [LoggerMessage(11, LogLevel.Warning, "Saga {SagaId} ({SagaType}) is compensating: {FailureKind} {Error}")]
  public static partial void SagaCompensating(ILogger logger, Guid sagaId, string sagaType, SagaFailureKind failureKind, string error);

  [LoggerMessage(12, LogLevel.Error, "Saga {SagaId} has type {SagaType}, which isn't registered; trying again in {Delay}")]
  public static partial void UnknownSagaType(ILogger logger, Guid sagaId, string sagaType, TimeSpan delay);

  [LoggerMessage(13, LogLevel.Information, "Saga {SagaId} changed or its lease was lost while it ran; its changes were dropped: {Reason}")]
  public static partial void ConcurrencyConflict(ILogger logger, Guid sagaId, string reason);

  [LoggerMessage(14, LogLevel.Error, "Running saga {SagaId} ({SagaType}) failed; it runs again when it is next claimed")]
  public static partial void RunFailed(ILogger logger, Exception exception, Guid sagaId, string sagaType);

  [LoggerMessage(15, LogLevel.Information, "Stopped running saga {SagaId} because the host is stopping")]
  public static partial void RunAborted(ILogger logger, Guid sagaId);

  [LoggerMessage(20, LogLevel.Warning, "Step {StepName} of saga {SagaId} failed (attempt {Attempt}); retrying in {Delay}")]
  public static partial void StepRetrying(ILogger logger, Exception exception, string stepName, Guid sagaId, int attempt, TimeSpan delay);

  [LoggerMessage(21, LogLevel.Warning, "Step {StepName} of saga {SagaId} failed: {Error}")]
  public static partial void StepFailed(ILogger logger, string stepName, Guid sagaId, string error);

  [LoggerMessage(22, LogLevel.Warning, "Compensating step {StepName} of saga {SagaId} failed (attempt {Attempt})")]
  public static partial void CompensationAttemptFailed(ILogger logger, Exception exception, string stepName, Guid sagaId, int attempt);

  [LoggerMessage(23, LogLevel.Error, "Compensating step {StepName} of saga {SagaId} failed after all retries; the saga needs manual action")]
  public static partial void CompensationFailed(ILogger logger, string stepName, Guid sagaId);

  [LoggerMessage(30, LogLevel.Warning, "Renewing the lease on saga {SagaId} failed")]
  public static partial void LeaseRenewalFailed(ILogger logger, Exception exception, Guid sagaId);

  [LoggerMessage(31, LogLevel.Warning, "The lease on saga {SagaId} was lost; stopping its run")]
  public static partial void LeaseLost(ILogger logger, Guid sagaId);

  [LoggerMessage(32, LogLevel.Warning, "Releasing the lease on saga {SagaId} failed; it expires on its own")]
  public static partial void LeaseReleaseFailed(ILogger logger, Exception exception, Guid sagaId);

  [LoggerMessage(40, LogLevel.Error, "Lifecycle observer {Observer} failed on {EventType} of saga {SagaId}")]
  public static partial void ObserverFailed(ILogger logger, Exception exception, string observer, SagaEventType eventType, Guid sagaId);

  [LoggerMessage(41, LogLevel.Error, "The completion hook of saga {SagaId} ({SagaType}) failed")]
  public static partial void HookFailed(ILogger logger, Exception exception, Guid sagaId, string sagaType);

  [LoggerMessage(50, LogLevel.Information, "Saga scheduler started with {Count} schedules")]
  public static partial void SchedulerStarted(ILogger logger, int count);

  [LoggerMessage(51, LogLevel.Information, "Saga scheduler is disabled (NativeOrchestrator:WorkerEnabled is false)")]
  public static partial void SchedulerDisabled(ILogger logger);

  [LoggerMessage(52, LogLevel.Information, "Schedule {ScheduleName} started saga {SagaId} for {ScheduledAtUtc:O}")]
  public static partial void OccurrenceStarted(ILogger logger, string scheduleName, Guid sagaId, DateTimeOffset scheduledAtUtc);

  [LoggerMessage(53, LogLevel.Debug, "Schedule {ScheduleName} occurrence {ScheduledAtUtc:O} was already started (saga {SagaId})")]
  public static partial void OccurrenceDuplicate(ILogger logger, string scheduleName, DateTimeOffset scheduledAtUtc, Guid sagaId);

  [LoggerMessage(54, LogLevel.Warning, "Schedule {ScheduleName} skipped {ScheduledAtUtc:O}: saga {SagaId} of the single-instance saga is still active or failed")]
  public static partial void OccurrenceSkipped(ILogger logger, string scheduleName, DateTimeOffset scheduledAtUtc, Guid sagaId);

  [LoggerMessage(55, LogLevel.Warning, "Schedule {ScheduleName} missed {Count} occurrences (later than its misfire threshold of {Threshold}, or superseded by a later one)")]
  public static partial void OccurrencesMisfired(ILogger logger, string scheduleName, int count, TimeSpan threshold);

  [LoggerMessage(56, LogLevel.Error, "Schedule {ScheduleName} failed to start {ScheduledAtUtc:O}; trying again in {Delay}")]
  public static partial void OccurrenceFailed(ILogger logger, Exception exception, string scheduleName, DateTimeOffset scheduledAtUtc, TimeSpan delay);

  [LoggerMessage(57, LogLevel.Error, "Schedule {ScheduleName} couldn't create valid data for {ScheduledAtUtc:O}; the occurrence is skipped")]
  public static partial void OccurrenceRejected(ILogger logger, Exception exception, string scheduleName, DateTimeOffset scheduledAtUtc);
}
