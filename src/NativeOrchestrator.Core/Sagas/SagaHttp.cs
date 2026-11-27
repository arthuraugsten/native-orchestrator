namespace NativeOrchestrator;

/// <summary>HTTP conventions shared by <see cref="HttpSagaStep{TData}"/> and <see cref="HttpPollingSagaStep{TData}"/>.</summary>
public static class SagaHttp
{
  /// <summary>The request header that carries <see cref="StepContext{TData}.IdempotencyKey"/>.</summary>
  public const string IdempotencyKeyHeader = "Idempotency-Key";

  /// <summary>
  /// Throws if a response failed: <c>408</c>, <c>429</c> and <c>5xx</c> throw <see cref="HttpRequestException"/>
  /// (transient: the step is retried); other <c>4xx</c> throw <see cref="StepFailedException"/> (permanent).
  /// </summary>
  /// <param name="response">The response.</param>
  public static void ThrowIfFailed(HttpResponseMessage response)
  {
    ArgumentNullException.ThrowIfNull(response);
    if (response.IsSuccessStatusCode)
      return;
    var code = (int)response.StatusCode;
    var message = $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} returned {code} {response.ReasonPhrase}.";
    if (code is 408 or 429 or >= 500)
      throw new HttpRequestException(message, null, response.StatusCode);
    throw new StepFailedException(message);
  }
}
