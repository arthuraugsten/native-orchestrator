using System.Text.Json;

namespace NativeOrchestrator;

/// <summary>A read-only view of a saga, as returned by <see cref="ISagaClient.GetAsync"/>.</summary>
/// <param name="Id">The saga's identifier.</param>
/// <param name="SagaType">The registered saga type name.</param>
/// <param name="CorrelationId">The caller's correlation identifier, if one was given.</param>
/// <param name="Status">The saga's status.</param>
/// <param name="CurrentStep">The step the saga is running or compensating, if any.</param>
/// <param name="Data">The saga's data, as JSON.</param>
/// <param name="CreatedAtUtc">When the saga was created.</param>
/// <param name="StartedAtUtc">When the first step started.</param>
/// <param name="CompletedAtUtc">When the saga finished.</param>
/// <param name="NextRunAtUtc">When the saga is next due to run, if it is still running.</param>
/// <param name="CancellationRequested">Whether cancellation was requested.</param>
/// <param name="Failure">Why the saga failed, if it did.</param>
/// <param name="Steps">The steps that have started, in declaration order.</param>
public sealed record SagaSnapshot(
  Guid Id,
  string SagaType,
  string? CorrelationId,
  SagaStatus Status,
  string? CurrentStep,
  JsonElement Data,
  DateTimeOffset CreatedAtUtc,
  DateTimeOffset? StartedAtUtc,
  DateTimeOffset? CompletedAtUtc,
  DateTimeOffset? NextRunAtUtc,
  bool CancellationRequested,
  SagaFailure? Failure,
  IReadOnlyList<StepSnapshot> Steps);
