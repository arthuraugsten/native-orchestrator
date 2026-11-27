using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace NativeOrchestrator.Tests;

public sealed class HttpPollingSagaStepTests
{
  [Fact]
  public async Task It_starts_with_an_idempotency_key_follows_the_relative_location_and_honours_retry_after()
  {
    var carrier = new FakeCarrier { PendingChecks = 1 };
    await using var host = CreateHost(carrier);
    var started = await host.Client.StartAsync("label", new TestData());

    var saga = await host.RunUntilFinishedAsync(started.SagaId);

    Assert.Equal(SagaStatus.Completed, saga.Status);
    Assert.Equal(["TN-1"], saga.Data.GetProperty("trace").EnumerateArray().Select(e => e.GetString()));
    Assert.Equal(
      ["POST https://carrier.test/labels", "GET https://carrier.test/labels/1", "GET https://carrier.test/labels/1"],
      carrier.Requests.Select(r => r.Request));
    Assert.Equal($"{started.SagaId:N}:Label", carrier.Requests[0].IdempotencyKey);
    // First check after the polling interval (1 minute), the second after Retry-After (30 seconds).
    Assert.Equal([60, 90], carrier.Requests.Skip(1).Select(r => (r.At - SagaTestHost.Start).TotalSeconds));
  }

  [Fact]
  public async Task A_client_error_on_start_fails_the_step_without_retries()
  {
    var carrier = new FakeCarrier { StartStatus = HttpStatusCode.UnprocessableEntity };
    await using var host = CreateHost(carrier);
    var started = await host.Client.StartAsync("label", new TestData());

    var saga = await host.RunUntilFinishedAsync(started.SagaId);

    Assert.Equal(SagaStatus.Compensated, saga.Status);
    Assert.Single(carrier.Requests);
    Assert.Contains("422", saga.Failure?.Error);
  }

  [Fact]
  public async Task A_server_error_on_a_check_is_retried()
  {
    var carrier = new FakeCarrier { FailingChecks = 1 };
    await using var host = CreateHost(carrier);
    var started = await host.Client.StartAsync("label", new TestData());

    var saga = await host.RunUntilFinishedAsync(started.SagaId);

    Assert.Equal(SagaStatus.Completed, saga.Status);
    Assert.Equal(3, carrier.Requests.Count);
    Assert.Contains(await host.Client.GetHistoryAsync(started.SagaId), h => h.EventType == SagaEventType.StepRetryScheduled);
  }

  private static SagaTestHost CreateHost(FakeCarrier carrier) =>
    SagaTestHost.Create(
      new SagaShape().Step("Unused"),
      configureServices: services =>
      {
        services.AddSingleton(carrier);
        services.AddHttpClient("carrier", client => client.BaseAddress = new Uri("https://carrier.test/"))
          .ConfigurePrimaryHttpMessageHandler(provider => new FakeCarrierHandler(provider.GetRequiredService<FakeCarrier>(), provider.GetRequiredService<TimeProvider>()));
      },
      configureOrchestrator: builder => builder.AddSaga<LabelSaga, TestData>("label", options =>
      {
        options.Retry = new RetryOptions { MaxAttempts = 3, InitialDelay = TimeSpan.FromSeconds(5), UseJitter = false };
        options.Polling = new PollingOptions { InitialDelay = TimeSpan.FromMinutes(1), Multiplier = 1, MaxDelay = TimeSpan.FromMinutes(1) };
      }));

  private sealed class LabelSaga : Saga<TestData>
  {
    protected override void Configure(ISagaBuilder<TestData> saga) => saga.Step<LabelStep>();
  }

  private sealed class LabelStep : HttpPollingSagaStep<TestData>
  {
    public LabelStep(IHttpClientFactory httpClientFactory)
      : base(httpClientFactory)
    {
    }

    protected override string ClientName => "carrier";

    protected override HttpRequestMessage CreateStartRequest(StepContext<TestData> context) =>
      new(HttpMethod.Post, "labels") { Content = JsonContent.Create(new { sku = "BOOK" }) };

    protected override async Task<PollResult> ReadStatusAsync(StepContext<TestData> context, HttpResponseMessage response, CancellationToken cancellationToken)
    {
      var body = await response.Content.ReadFromJsonAsync<LabelResponse>(cancellationToken);
      context.Data.Trace.Add(body!.TrackingNumber);
      return PollResult.Succeeded();
    }
  }

  private sealed record LabelResponse(string TrackingNumber);

  private sealed class FakeCarrier
  {
    public HttpStatusCode StartStatus { get; init; } = HttpStatusCode.Accepted;

    public int PendingChecks { get; set; }

    public int FailingChecks { get; set; }

    public List<(string Request, string? IdempotencyKey, DateTimeOffset At)> Requests { get; } = [];
  }

  private sealed class FakeCarrierHandler : HttpMessageHandler
  {
    private readonly FakeCarrier _carrier;
    private readonly TimeProvider _timeProvider;

    public FakeCarrierHandler(FakeCarrier carrier, TimeProvider timeProvider)
    {
      _carrier = carrier;
      _timeProvider = timeProvider;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var key = request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null;
      _carrier.Requests.Add(($"{request.Method} {request.RequestUri}", key, _timeProvider.GetUtcNow()));

      HttpResponseMessage response;
      if (request.Method == HttpMethod.Post)
      {
        response = new HttpResponseMessage(_carrier.StartStatus);
        response.Headers.Location = new Uri("/labels/1", UriKind.Relative);
      }
      else if (_carrier.FailingChecks > 0)
      {
        _carrier.FailingChecks--;
        response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
      }
      else if (_carrier.PendingChecks > 0)
      {
        _carrier.PendingChecks--;
        response = new HttpResponseMessage(HttpStatusCode.Accepted);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
      }
      else
      {
        response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { trackingNumber = "TN-1" }) };
      }

      response.RequestMessage = request;
      return Task.FromResult(response);
    }
  }
}
