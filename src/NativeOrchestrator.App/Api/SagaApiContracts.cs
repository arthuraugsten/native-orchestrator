using System.Text.Json;

namespace NativeOrchestrator.App.Api;

/// <summary>The body of <c>POST /sagas/{sagaType}</c>.</summary>
/// <param name="Data">The saga's input data.</param>
/// <param name="CorrelationId">Optional. Starting again with the same value returns the existing saga instead of a new one.</param>
public sealed record StartSagaRequest(JsonElement Data, string? CorrelationId);

/// <summary>The answer to <c>POST /sagas/{sagaType}</c>.</summary>
/// <param name="SagaId">The saga's identifier, or the existing saga's if none was created.</param>
/// <param name="Created">Whether a new saga was created.</param>
/// <param name="Outcome">
/// <see cref="SagaStartOutcome.Duplicate"/> if the correlation ID was already used; <see cref="SagaStartOutcome.AlreadyRunning"/>
/// if the saga type is single-instance and one is active.
/// </param>
/// <param name="StatusUrl">Where to read the saga's status.</param>
public sealed record StartSagaResponse(Guid SagaId, bool Created, SagaStartOutcome Outcome, string StatusUrl);

/// <summary>The answer to <c>POST /sagas/{id}/cancel</c>.</summary>
/// <param name="SagaId">The saga's identifier.</param>
/// <param name="Result">What happened.</param>
public sealed record CancelSagaResponse(Guid SagaId, CancellationRequestResult Result);
