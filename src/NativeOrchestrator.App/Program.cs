using NativeOrchestrator;
using NativeOrchestrator.App;
using NativeOrchestrator.App.Api;
using NativeOrchestrator.App.ExternalServices;
using NativeOrchestrator.App.Observability;
using NativeOrchestrator.App.Sagas.CustomerOnboarding;
using NativeOrchestrator.App.Sagas.ReportGeneration;
using NativeOrchestrator.App.Simulated;
using NativeOrchestrator.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));
builder.Services.AddProblemDetails();

// The orchestrator: options from "NativeOrchestrator" (worker), "NativeOrchestrator:SagaDefaults" and
// "NativeOrchestrator:Sagas:{name}" (per saga and step), sagas stored in SQL Server.
builder.Services.AddNativeOrchestrator(configuration.GetSection("NativeOrchestrator"))
  .AddSaga<CustomerOnboardingSaga, CustomerOnboardingData>(CustomerOnboardingSaga.Name)
  // One report run at a time: a start while one is active (or failed, awaiting manual action) answers 409.
  .AddSaga<ReportGenerationSaga, ReportGenerationData>(ReportGenerationSaga.Name, saga => saga.SingleInstance = true)
  // The daily sales report at 02:00 UTC. "NativeOrchestrator:Schedules:nightly-sales" can change the CRON expression,
  // time zone or misfire threshold, or pause it; Development runs it every 2 minutes.
  .AddSchedule("nightly-sales", ReportGenerationSaga.Name, "0 2 * * *", occurrence => new ReportGenerationData
  {
    ReportName = $"Daily sales {occurrence.ScheduledAtUtc.AddDays(-1):yyyy-MM-dd}",
    Recipients = ["finance@example.test"],
  })
  .UseSqlServer(
    configuration.GetSection("NativeOrchestrator:SqlServer"),
    sql => sql.ConnectionString = configuration.GetConnectionString("Sagas") ?? "")
  .ConfigureSerializer(json => json.TypeInfoResolverChain.Insert(0, AppJsonContext.Default))
  .AddObserver<LoggingSagaObserver>();

// External systems, each a named HttpClient with Microsoft.Extensions.Http.Resilience's standard pipeline.
builder.Services.AddExternalServices(configuration.GetSection("ExternalServices"), configuration.GetSection("Resilience"));
builder.Services.AddSimulatedExternalSystems(configuration.GetSection("Simulated"));

builder.Services.AddHealthChecks().AddDbContextCheck<SagaDbContext>("saga-store");

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapSagaEndpoints();
app.MapSimulatedExternalSystems();
app.MapHealthChecks("/health");

app.Run();
