using Microsoft.AspNetCore.Http.HttpResults;

namespace NativeOrchestrator.App.Api;

/// <summary>The HTTP API that starts, reads and cancels sagas. Callers poll the status URL until the saga finishes.</summary>
public static class SagaEndpoints
{
  public static IEndpointRouteBuilder MapSagaEndpoints(this IEndpointRouteBuilder endpoints)
  {
    var sagas = endpoints.MapGroup("/sagas").WithTags("Sagas");
    sagas.MapPost("/{sagaType}", StartAsync).WithName("StartSaga");
    sagas.MapGet("/{id:guid}", GetAsync).WithName("GetSaga");
    sagas.MapGet("/{id:guid}/history", GetHistoryAsync).WithName("GetSagaHistory");
    sagas.MapPost("/{id:guid}/cancel", CancelAsync).WithName("CancelSaga");
    return endpoints;
  }

  private static async Task<Results<Accepted<StartSagaResponse>, Conflict<StartSagaResponse>, ProblemHttpResult>> StartAsync(
    string sagaType,
    StartSagaRequest request,
    ISagaClient client,
    CancellationToken cancellationToken)
  {
    try
    {
      var result = await client.StartAsync(sagaType, request.Data, request.CorrelationId, cancellationToken);
      var statusUrl = $"/sagas/{result.SagaId}";
      var response = new StartSagaResponse(result.SagaId, result.Created, result.Outcome, statusUrl);
      // A single-instance saga is already running: nothing was started with this request's data.
      if (result.Outcome == SagaStartOutcome.AlreadyRunning)
        return TypedResults.Conflict(response);
      return TypedResults.Accepted(statusUrl, response);
    }
    catch (UnknownSagaTypeException ex)
    {
      return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Unknown saga type");
    }
    catch (ArgumentException ex)
    {
      return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Invalid saga data");
    }
  }

  private static async Task<Results<Ok<SagaSnapshot>, NotFound>> GetAsync(Guid id, ISagaClient client, CancellationToken cancellationToken)
  {
    var saga = await client.GetAsync(id, cancellationToken);
    return saga is null ? TypedResults.NotFound() : TypedResults.Ok(saga);
  }

  private static async Task<Results<Ok<IReadOnlyList<SagaHistoryEntry>>, NotFound>> GetHistoryAsync(
    Guid id,
    ISagaClient client,
    CancellationToken cancellationToken)
  {
    var history = await client.GetHistoryAsync(id, cancellationToken);
    return history.Count == 0 ? TypedResults.NotFound() : TypedResults.Ok(history);
  }

  private static async Task<Results<Accepted<CancelSagaResponse>, Conflict<CancelSagaResponse>, NotFound>> CancelAsync(
    Guid id,
    ISagaClient client,
    CancellationToken cancellationToken)
  {
    var result = await client.CancelAsync(id, cancellationToken);
    var response = new CancelSagaResponse(id, result);
    return result switch
    {
      CancellationRequestResult.NotFound => TypedResults.NotFound(),
      CancellationRequestResult.NotCancellable => TypedResults.Conflict(response),
      _ => TypedResults.Accepted($"/sagas/{id}", response),
    };
  }
}
