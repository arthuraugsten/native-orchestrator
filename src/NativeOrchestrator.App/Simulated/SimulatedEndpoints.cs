using System.Globalization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace NativeOrchestrator.App.Simulated;

/// <summary>
/// HTTP endpoints that stand in for the external systems: an accounts API, an identity verification API and a report
/// API that both follow the asynchronous request-reply pattern (<c>202</c> + <c>Location</c>), and an email API.
/// </summary>
public static class SimulatedEndpoints
{
  public static IServiceCollection AddSimulatedExternalSystems(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddOptions<SimulatedOptions>().Bind(configuration);
    services.AddSingleton<SimulatedExternalSystems>();

    return services;
  }

  public static IEndpointRouteBuilder MapSimulatedExternalSystems(this IEndpointRouteBuilder endpoints)
  {
    if (!endpoints.ServiceProvider.GetRequiredService<IOptions<SimulatedOptions>>().Value.Enabled)
      return endpoints;

    var simulated = endpoints.MapGroup("/simulated").WithTags("Simulated external systems").ExcludeFromDescription();

    simulated.MapPost("/accounts", CreateAccount);
    simulated.MapPost("/accounts/{id}/activate", ActivateAccount);
    simulated.MapDelete("/accounts/{id}", DeleteAccount);

    simulated.MapPost("/identity/verifications", StartVerification);
    simulated.MapGet("/identity/verifications/{id}", GetVerification);

    simulated.MapPost("/reports", StartReport);
    simulated.MapGet("/reports/{id}", GetReport);
    simulated.MapDelete("/reports/{id}", DiscardReport);

    simulated.MapPost("/notifications/email", SendEmail);
    simulated.MapGet("/notifications/email", (SimulatedExternalSystems systems) => TypedResults.Ok(systems.Emails.ToArray()));
    return endpoints;
  }

  private static Results<Created<AccountResponse>, ProblemHttpResult> CreateAccount(
    CreateAccountRequest request,
    HttpRequest http,
    SimulatedExternalSystems systems)
  {
    var key = IdempotencyKey(http);

    if (!systems.PassFlakyCheck(request.Email, key))
      return TypedResults.Problem("The accounts database is busy.", statusCode: StatusCodes.Status503ServiceUnavailable);

    var account = systems.CreateAccount(request, key);

    return TypedResults.Created($"/simulated/accounts/{account.Id}", new AccountResponse(account.Id, account.Active ? "active" : "pending"));
  }

  private static Results<Ok<AccountResponse>, NotFound> ActivateAccount(string id, SimulatedExternalSystems systems)
  {
    var account = systems.FindAccount(id);

    if (account is null)
      return TypedResults.NotFound();

    account.Active = true;

    return TypedResults.Ok(new AccountResponse(account.Id, "active"));
  }

  // Deleting is idempotent: a compensation that is repeated still succeeds.
  private static NoContent DeleteAccount(string id, SimulatedExternalSystems systems)
  {
    systems.DeleteAccount(id);

    return TypedResults.NoContent();
  }

  private static Accepted StartVerification(VerificationRequest request, HttpRequest http, SimulatedExternalSystems systems)
  {
    var verification = systems.StartVerification(request, IdempotencyKey(http));

    return TypedResults.Accepted($"/simulated/identity/verifications/{verification.Id}");
  }

  private static IResult GetVerification(string id, HttpResponse http, SimulatedExternalSystems systems)
  {
    var status = systems.GetVerification(id);

    if (status is null)
      return TypedResults.NotFound();

    if (status.Status == "pending")
      return Pending(http, systems);

    return TypedResults.Ok(status);
  }

  private static Accepted StartReport(ReportRequest request, HttpRequest http, SimulatedExternalSystems systems)
  {
    var report = systems.StartReport(request, IdempotencyKey(http));
    return TypedResults.Accepted($"/simulated/reports/{report.Id}");
  }

  private static IResult GetReport(string id, HttpRequest request, HttpResponse http, SimulatedExternalSystems systems)
  {
    var status = systems.GetReport(id, $"{request.Scheme}://{request.Host}");

    if (status is null)
      return TypedResults.NotFound();

    if (status.Status == "running")
      return Pending(http, systems);

    return TypedResults.Ok(status);
  }

  private static NoContent DiscardReport(string id, SimulatedExternalSystems systems)
  {
    systems.DiscardReport(id);

    return TypedResults.NoContent();
  }

  private static Results<Accepted, ProblemHttpResult> SendEmail(EmailRequest request, SimulatedExternalSystems systems)
  {
    if (request.To.EndsWith(SimulatedTriggers.BouncingEmailSuffix, StringComparison.OrdinalIgnoreCase))
      return TypedResults.Problem($"The mailbox {request.To} doesn't exist.", statusCode: StatusCodes.Status422UnprocessableEntity);

    systems.SendEmail(request);

    return TypedResults.Accepted((string?)null);
  }

  private static Accepted Pending(HttpResponse http, SimulatedExternalSystems systems)
  {
    http.Headers.RetryAfter = ((int)Math.Ceiling(systems.RetryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
    return TypedResults.Accepted((string?)null);
  }

  private static string IdempotencyKey(HttpRequest http) =>
    http.Headers[SagaHttp.IdempotencyKeyHeader].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
}
