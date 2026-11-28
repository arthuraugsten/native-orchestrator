namespace NativeOrchestrator;

/// <summary>
/// The base class for a step that calls an HTTP API and finishes in one call. <see cref="SendAsync"/> adds the
/// idempotency key and turns failed responses into step failures, following <see cref="SagaHttp.ThrowIfFailed"/>.
/// </summary>
/// <typeparam name="TData">The saga's data.</typeparam>
/// <remarks>
/// Requests go through the named <see cref="HttpClient"/> <see cref="ClientName"/>. Add in-call resilience to that
/// client, for example with <c>AddStandardResilienceHandler()</c>.
/// </remarks>
public abstract class HttpSagaStep<TData> : SagaStep<TData>
  where TData : class
{
  private readonly IHttpClientFactory _httpClientFactory;

  /// <summary>Creates the step.</summary>
  /// <param name="httpClientFactory">Creates the named client.</param>
  protected HttpSagaStep(IHttpClientFactory httpClientFactory)
  {
    ArgumentNullException.ThrowIfNull(httpClientFactory);
    _httpClientFactory = httpClientFactory;
  }

  /// <summary>The name of the <see cref="HttpClient"/> to use.</summary>
  protected abstract string ClientName { get; }

  /// <summary>
  /// Sends a request with the <c>Idempotency-Key</c> header, and returns the response if it succeeded. Dispose the
  /// response when done.
  /// </summary>
  /// <param name="request">The request.</param>
  /// <param name="context">The step's context.</param>
  /// <param name="cancellationToken">Cancels the request.</param>
  /// <exception cref="HttpRequestException">A transient failure (<c>408</c>, <c>429</c>, <c>5xx</c>): the step is retried.</exception>
  /// <exception cref="StepFailedException">Any other <c>4xx</c>: the step fails.</exception>
  protected async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, StepContext<TData> context, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);
    ArgumentNullException.ThrowIfNull(context);
    request.Headers.TryAddWithoutValidation(SagaHttp.IdempotencyKeyHeader, context.IdempotencyKey);
    var response = await _httpClientFactory.CreateClient(ClientName).SendAsync(request, cancellationToken).ConfigureAwait(false);
    try
    {
      SagaHttp.ThrowIfFailed(response);
      return response;
    }
    catch
    {
      response.Dispose();
      throw;
    }
  }
}
