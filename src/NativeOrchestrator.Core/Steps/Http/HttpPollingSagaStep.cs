using System.Net;
using System.Net.Http.Headers;

namespace NativeOrchestrator;

/// <summary>
/// A polling step for the HTTP asynchronous request-reply pattern: the start request is answered with
/// <c>202 Accepted</c> and a <c>Location</c> header; polling that URL returns <c>202</c> while the operation runs and
/// <c>200</c> when it has finished.
/// </summary>
/// <typeparam name="TData">The saga's data.</typeparam>
/// <remarks>
/// <para>
/// Requests go through the named <see cref="HttpClient"/> <see cref="ClientName"/>. Add in-call resilience
/// (timeouts, short retries, circuit breaker) to that client, for example with
/// <c>AddStandardResilienceHandler()</c>. The start request carries an <c>Idempotency-Key</c> header.
/// </para>
/// <para>
/// Responses: <c>202</c> keeps polling (honouring <c>Retry-After</c>). Other <c>2xx</c> responses go to
/// <see cref="ReadStatusAsync"/>. <c>408</c>, <c>429</c> and <c>5xx</c> throw, so the step is retried. Other
/// <c>4xx</c> responses fail the step.
/// </para>
/// </remarks>
public abstract class HttpPollingSagaStep<TData> : PollingSagaStep<TData, HttpPollingOperation>
  where TData : class
{

  private readonly IHttpClientFactory _httpClientFactory;

  /// <summary>Creates the step.</summary>
  /// <param name="httpClientFactory">Creates the named client.</param>
  protected HttpPollingSagaStep(IHttpClientFactory httpClientFactory)
  {
    ArgumentNullException.ThrowIfNull(httpClientFactory);
    _httpClientFactory = httpClientFactory;
  }

  /// <summary>The name of the <see cref="HttpClient"/> to use.</summary>
  protected abstract string ClientName { get; }

  /// <summary>Creates the request that starts the operation.</summary>
  /// <param name="context">The step's context.</param>
  protected abstract HttpRequestMessage CreateStartRequest(StepContext<TData> context);

  /// <summary>
  /// Reads a finished status response (any <c>2xx</c> other than <c>202</c>): returns whether the operation succeeded
  /// or failed, and copies any result into <see cref="StepContext{TData}.Data"/>.
  /// </summary>
  /// <param name="context">The step's context.</param>
  /// <param name="response">The status response.</param>
  /// <param name="cancellationToken">Cancels the operation.</param>
  protected abstract Task<PollResult> ReadStatusAsync(StepContext<TData> context, HttpResponseMessage response, CancellationToken cancellationToken);

  /// <summary>Creates the status request. Override it to add headers.</summary>
  /// <param name="context">The step's context.</param>
  /// <param name="statusUri">The status URL.</param>
  protected virtual HttpRequestMessage CreateStatusRequest(StepContext<TData> context, Uri statusUri) =>
    new(HttpMethod.Get, statusUri);

  /// <inheritdoc />
  protected sealed override async Task<HttpPollingOperation> StartAsync(StepContext<TData> context, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(context);
    using var request = CreateStartRequest(context);
    request.Headers.TryAddWithoutValidation(SagaHttp.IdempotencyKeyHeader, context.IdempotencyKey);
    var client = _httpClientFactory.CreateClient(ClientName);
    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

    SagaHttp.ThrowIfFailed(response);
    if (response.StatusCode != HttpStatusCode.Accepted)
      throw new StepFailedException($"Expected 202 Accepted from {request.RequestUri}, got {(int)response.StatusCode}.");
    var location = response.Headers.Location
      ?? throw new StepFailedException($"The 202 response from {request.RequestUri} has no Location header.");

    var requestUri = response.RequestMessage?.RequestUri ?? request.RequestUri;
    var statusUri = location.IsAbsoluteUri || requestUri is null ? location : new Uri(requestUri, location);
    return new HttpPollingOperation(statusUri);
  }

  /// <inheritdoc />
  protected sealed override async Task<PollResult> CheckAsync(StepContext<TData> context, HttpPollingOperation operation, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(operation);
    using var request = CreateStatusRequest(context, operation.StatusUri);
    var client = _httpClientFactory.CreateClient(ClientName);
    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

    SagaHttp.ThrowIfFailed(response);
    if (response.StatusCode == HttpStatusCode.Accepted)
      return PollResult.Pending(GetRetryAfter(response.Headers.RetryAfter, context.TimeProvider));
    return await ReadStatusAsync(context, response, cancellationToken).ConfigureAwait(false);
  }


  private static TimeSpan? GetRetryAfter(RetryConditionHeaderValue? retryAfter, TimeProvider timeProvider)
  {
    if (retryAfter?.Delta is { } delta)
      return delta;
    if (retryAfter?.Date is { } date)
    {
      var delay = date - timeProvider.GetUtcNow();
      return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
    }

    return null;
  }
}
