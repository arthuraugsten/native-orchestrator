using NativeOrchestrator;
using NativeOrchestrator.App;
using NativeOrchestrator.App.Api;
using NativeOrchestrator.App.ApiManagement;
using NativeOrchestrator.App.Observability;
using NativeOrchestrator.App.Sagas.Client;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));
builder.Services.AddProblemDetails();

builder.Services.AddSagaOrchestrator(configuration.GetSection("SagaOrchestrator"))
  .AddSaga<ClientSaga, ClientSagaData>(ClientSaga.Name, saga =>
  {
    saga.SingleInstance = true;
    saga.FailedBlocksNewStarts = false;
  })
  .AddSchedule(ClientSaga.ScheduleName, ClientSaga.Name, "*/5 * * * *", occurrence => new ClientSagaData
  {
    RunName = $"client-{occurrence.ScheduledAtUtc:yyyyMMdd-HHmm}",
    ScheduledAtUtc = occurrence.ScheduledAtUtc,
  })
  .UseSqlServer(
    configuration.GetSection("SagaOrchestrator:SqlServer"),
    sql => sql.ConnectionString = configuration.GetConnectionString("Sagas") ?? "")
  .ConfigureSerializer(json => json.TypeInfoResolverChain.Insert(0, AppJsonContext.Default))
  .AddObserver<LoggingSagaObserver>();

builder.Services.AddApiManagement(configuration.GetSection("ApiManagement"));
builder.Services.AddHealthChecks().AddSqlServerSagaStore();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapSagaEndpoints();
app.MapHealthChecks("/health");

app.Run();
