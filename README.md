# NativeOrchestrator

A saga orchestrator for long-running work, built on the .NET Generic Host. It is a central service: it calls external
systems, polls them until they finish, and compensates the steps that succeeded when a later one fails. Every state
change is saved to a database, so a saga can run for days and survive restarts and deployments.

- **Base classes** for sagas and steps (plain, polling, HTTP), with a typed data object per saga.
- **Durable polling, retries and timeouts.** Nothing waits in memory: a saga that polls every hour for two days is a
  database row with a due time.
- **Storage that doesn't depend on the database:** the engine depends on two interfaces, implemented on EF Core (any
  relational provider), with SQL Server as the shipped provider.
- **Built-in .NET first:** Generic Host, `BackgroundService`, options with validation, `TimeProvider`,
  System.Text.Json source generation, `IHttpClientFactory`, `[LoggerMessage]`, `Meter` and `ActivitySource`, health
  checks, EF Core and Microsoft.Extensions.Http.Resilience.

## Projects

| Project | Kind | What it holds |
| --- | --- | --- |
| `src/NativeOrchestrator.Core` | class library | Base classes (`Saga<TData>`, `SagaStep<TData>`, `PollingSagaStep<TData, TOperation>`, `HttpSagaStep<TData>`, `HttpPollingSagaStep<TData>`), the engine, the worker, `ISagaClient`, options, retry and backoff policies, and the store contracts (`ISagaRepository`, `ISagaWorkQueue`, `SagaRecord`). No database code. |
| `src/NativeOrchestrator.Data.SqlServer` | class library | The SQL Server store: `UseSqlServer(...)`, its health check, the migrations and a locking claim query. Built on EF Core, which stays internal. |
| `src/NativeOrchestrator.Data.InMemory` | class library | `UseInMemoryStore()`: a store in memory, for tests and local experiments. |
| `src/NativeOrchestrator.App` | ASP.NET Core app | The orchestrator service: HTTP API, worker, two example sagas and simulated external systems. |
| `tests/NativeOrchestrator.Core.Tests` | xUnit v3 | Engine, polling, retries, compensation, options and worker, on a fake clock. |
| `tests/NativeOrchestrator.Data.SqlServer.Tests` | xUnit v3 | The store contract and the engine against SQL Server in a Testcontainers container. |

`tests/Shared` holds the store contract tests and test helpers, compiled into both test projects.

**Interactive docs:** open [`docs/index.html`](docs/index.html) in a browser for the design principles, workflows,
activity diagrams, the class dependency tree and an example for every feature.

## Run it locally

You need the .NET 11 SDK (see the repository's `global.json`) and Docker.

```sh
cd NativeOrchestrator
docker compose up -d                      # SQL Server 2022 on localhost:1433
dotnet run --project src/NativeOrchestrator.App
```

In Development the app applies the migrations on startup and listens on `http://localhost:5080`. Send the requests in
[`NativeOrchestrator.App.http`](src/NativeOrchestrator.App/NativeOrchestrator.App.http) (VS Code REST Client or Visual
Studio): one per scenario, each with its status request.

### HTTP API

| Request | Answer |
| --- | --- |
| `POST /sagas/{sagaType}` with `{ "data": {...}, "correlationId": "..." }` | `202` with `{ sagaId, created, outcome, statusUrl }`. The same correlation ID returns the existing saga (`created: false`, `outcome: "Duplicate"`). For a single-instance saga that is already active (or failed), `409` with that saga (`outcome: "AlreadyRunning"`). `400` for invalid data, `404` for an unknown type. |
| `GET /sagas/{id}` | The saga: status, current step, data, steps (with checks and errors), failure. |
| `GET /sagas/{id}/history` | Its history, oldest first. |
| `POST /sagas/{id}/cancel` | `202` (requested), `409` (finished or already compensating), `404`. |
| `GET /health` | Checks the saga database. |

## How it works

```
 POST /sagas/{type} ─► ISagaClient ─► store: new row (Pending, due now) ─► wakes the local worker

 SagaWorker (every instance, up to MaxConcurrency sagas at once)
   ┌──────────────────────────────────────────────────────────────────────────────┐
   │ 1. ClaimDue: lease due sagas (UPDLOCK, READPAST on SQL Server)               │
   │ 2. SagaEngine: run steps until one must wait; save after each step           │
   │    (LeaseKeeper renews the lease meanwhile)                                  │
   │ 3. Final save: status, NextRunAtUtc, history; release the lease              │
   └──────────────────────────────────────────────────────────────────────────────┘
```

**Saga states**

```
Pending ─► Running ⇄ Waiting ─► Completed
              │
              ├─ step failed / step timed out / saga timed out / cancelled
              ▼
        Compensating ─► Compensated          (every succeeded step undone, latest first)
              └───────► Failed               (a compensation failed after its retries: needs manual action)
```

**A step's run** (`SagaStepExecutor`):

| The step... | Then |
| --- | --- |
| returns `StepResult.Success()` | Saved; the next step runs in the same run. |
| returns `StepResult.Poll(delay?)` | Saved as `Waiting`, due after the polling delay (or `delay`, capped at `MaxDelay`). |
| throws (transient) | `IRetryPolicy` decides: due again after a backoff, or the step fails after `MaxAttempts`. |
| returns `StepResult.Failure(...)` or throws `StepFailedException` | Fails without retries. |
| runs past `StepOptions.Timeout` (measured from its first start) | Fails as `StepTimedOut`. |

A failed step compensates the saga, unless it has `ContinueOnError`. Compensations are retried with
`CompensationRetry`. If one still fails, the rest still run and the saga ends `Failed`.

**Guarantees.** A saga runs on one instance at a time: a lease plus a version check on every save. Cancellation bumps the
version, so a run that is in progress can't overwrite it. Steps run **at least once**: if an instance dies after calling an
external system but before saving, the step runs again. So every step gets `context.IdempotencyKey`
(`{sagaId}:{step}`, or `...:compensate`), and the HTTP base classes send it as an `Idempotency-Key` header.

## Writing a saga

```csharp
public sealed class CustomerOnboardingSaga : Saga<CustomerOnboardingData>
{
  protected override void Configure(ISagaBuilder<CustomerOnboardingData> saga) => saga
    .Step<ReserveAccountStep>()                                                  // HttpSagaStep, compensates by deleting
    .Step<VerifyIdentityStep>(configure: step => step.Timeout = TimeSpan.FromDays(2)) // HttpPollingSagaStep
    .Step<ActivateAccountStep>()
    .Step<SendWelcomeEmailStep>(configure: step => step.ContinueOnError = true);

  protected override string? Validate(CustomerOnboardingData data) =>
    string.IsNullOrWhiteSpace(data.Email) ? "email is required." : null;
}
```

```csharp
builder.Services.AddSagaOrchestrator(configuration.GetSection("SagaOrchestrator"))
  .AddSaga<CustomerOnboardingSaga, CustomerOnboardingData>("customer-onboarding")
  .UseSqlServer(configuration.GetSection("SagaOrchestrator:SqlServer"),
    sql => sql.ConnectionString = configuration.GetConnectionString("Sagas")!)
  .ConfigureSerializer(json => json.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));
```

Steps are created from DI on each run, so they take services in their constructors. Step and saga names are stored with
every saga and are configuration keys, so keep them stable once sagas are running (defaults: the class name without its
`Step` or `Saga` suffix). Changes to `context.Data` are saved when a step succeeds or polls, and dropped when it fails.

To allow only one active saga of a type at a time (for example a nightly sync that must not overlap itself), set
`SingleInstance`: in code (`AddSaga<...>("name", saga => saga.SingleInstance = true)`) or in configuration
(`Sagas:{name}:SingleInstance`). While one is pending, running, waiting or compensating, a start returns it with
`SagaStartOutcome.AlreadyRunning` and the new data is not used. Once it completes or compensates, the next start creates
a new saga. A saga that ends `Failed` (a compensation failed) keeps blocking by default, because it needs manual action
first. After fixing what it left behind, release it in SQL:
`UPDATE saga.Sagas SET ActiveInstanceKey = NULL WHERE Id = '<saga id>' AND Status = 'Failed'`. To let the next saga
start as soon as one fails instead, set `FailedBlocksNewStarts` to `false` (in code or as
`Sagas:{name}:FailedBlocksNewStarts`); it is read when the saga finishes. The store enforces all this with a unique key,
so it holds across instances and concurrent starts. A repeated correlation ID is checked first and still returns
`Duplicate`. `SingleInstance` applies to sagas started after it changes. In the app, `report-generation` is
single-instance, and a failed report blocks the next one.

| Base class | Use it for | You implement |
| --- | --- | --- |
| `SagaStep<TData>` | Anything | `ExecuteAsync`, and `CompensateAsync` if the step can be undone |
| `PollingSagaStep<TData, TOperation>` | Start an operation, then poll it | `StartAsync` (returns the operation, which is saved), `CheckAsync` |
| `HttpSagaStep<TData>` | One HTTP call | `ClientName`, `ExecuteAsync` using `SendAsync` |
| `HttpPollingSagaStep<TData>` | HTTP `202` + `Location` polling | `ClientName`, `CreateStartRequest`, `ReadStatusAsync` |

The HTTP base classes treat `408`, `429` and `5xx` as transient (retried) and other `4xx` as permanent failures.
`202` keeps polling and honours `Retry-After`.

## Scheduling

A saga can also start on a CRON schedule, declared at setup:

```csharp
.AddSaga<ReportGenerationSaga, ReportGenerationData>("report-generation", saga => saga.SingleInstance = true)
.AddSchedule("nightly-sales", "report-generation", "0 2 * * *", occurrence => new ReportGenerationData
{
  ReportName = $"Daily sales {occurrence.ScheduledAtUtc.AddDays(-1):yyyy-MM-dd}",
  Recipients = ["finance@example.test"],
}, schedule => schedule.TimeZone = "Europe/Lisbon")
```

The expression has five fields (`minute hour day-of-month month day-of-week`), or six with seconds first. Fields take
`*`, values, lists, ranges and steps (`*/15`, `1-5`, `0-30/10`); months and days also take names (`JAN`, `MON-FRI`).
Macros: `@yearly`, `@monthly`, `@weekly`, `@daily`, `@hourly`. When both day fields are restricted, either one matches,
as in standard cron. The data factory gets the occurrence (`ScheduleName`, `ScheduledAtUtc`), and the data still goes
through the saga's `Validate`.

| Case | What happens |
| --- | --- |
| An occurrence is due | Every instance with the worker enabled tries to start it, with the correlation ID `schedule:{name}:{yyyyMMddTHHmmssZ}`. The correlation index lets exactly one create the saga. |
| The app was down when an occurrence was due | On startup, only the latest missed occurrence starts, and only if it's within `MisfireThreshold` (default 1 hour). Older ones are skipped, never backfilled. |
| The saga is `SingleInstance` and one is still active | The occurrence is skipped (and logged), not queued. An instance that starts later, within the threshold, may still start it. |
| The store is unavailable | The occurrence is tried again every `ScanInterval` while it's within the threshold. |
| The factory throws or `Validate` rejects the data | The occurrence is skipped and logged; it isn't retried. |
| A daylight-saving change skips the time | It runs when the clocks change. |
| A daylight-saving change repeats the time | It runs once, the first time (an every-15-minutes schedule doesn't run in the repeated hour). |
| `Enabled: false` | Nothing starts. When it's turned back on, missed occurrences don't catch up. |

Nothing is stored for the schedule itself, so there is no extra table. The scheduler reads its options at every wake-up
(at least once a minute), so changing `Schedules:{name}` in configuration applies without a restart. An invalid
expression or time zone, an unknown saga or the wrong data type fails at startup. In the app, `nightly-sales` starts
`report-generation` at 02:00 UTC, and every 2 minutes in Development.

## Configuration

Options are bound from configuration and validated at startup. Each level overrides the one before it: defaults, then
`SagaDefaults`, then the saga's code, then `Sagas:{name}`. For steps and schedules, configuration wins over code.

```jsonc
"SagaOrchestrator": {
  "WorkerEnabled": true, "MaxConcurrency": 16, "ScanInterval": "00:00:05",
  "LeaseDuration": "00:05:00", "LeaseRenewalInterval": "00:01:00",
  "SqlServer": { "CommandTimeout": "00:00:30", "MaxRetryCount": 6, "UseLockingClaim": true, "ApplyMigrationsOnStartup": false },
  "SagaDefaults": {                            // SagaOptions for every saga
    "MaxDuration": "7.00:00:00", "StepTimeout": "1.00:00:00", "ExecutionTimeout": "00:02:00",
    "Retry":   { "MaxAttempts": 8, "InitialDelay": "00:00:10", "MaxDelay": "00:15:00", "Multiplier": 2, "UseJitter": true },
    "Polling": { "InitialDelay": "00:00:30", "MaxDelay": "00:10:00", "Multiplier": 1.5 },
    "CompensationRetry": { "MaxAttempts": 10 }
  },
  "Sagas": {
    "customer-onboarding": {                   // SagaOptions for one saga
      "MaxDuration": "3.00:00:00", "SingleInstance": false, "FailedBlocksNewStarts": true,
      "Steps": { "VerifyIdentity": { "Timeout": "2.00:00:00", "Polling": { "InitialDelay": "00:01:00", "MaxDelay": "00:30:00" } } }
    }
  },
  "Schedules": {
    "nightly-sales": {                         // SagaScheduleOptions for one schedule; configuration overrides code
      "Cron": "0 2 * * *", "TimeZone": "UTC", "Enabled": true, "MisfireThreshold": "01:00:00"
    }
  }
}
```

## Customising

| To change | Do this |
| --- | --- |
| Which steps run | `Configure` on your `Saga<TData>` |
| Behaviour after completion or failure | Override `OnCompletedAsync` / `OnFailedAsync` |
| Input validation | Override `Validate` |
| Which exceptions are retried | `UseRetryPolicy<T>()` with your `IRetryPolicy` |
| Delay curve | `UseBackoffStrategy<T>()` with your `IBackoffStrategy` |
| Serialization | `ConfigureSerializer(...)` or `UseSerializer<T>()` |
| Events (publish, alert) | `AddObserver<T>()` with an `ISagaLifecycleObserver` |
| Storage | `UseSqlServer(...)`, `UseInMemoryStore()`, or `UseStore<T>()` with a class implementing `ISagaRepository` and `ISagaWorkQueue` |
| How SQL Server claims sagas | `UseLockingClaim` in the SQL Server options |
| Clock | Register a `TimeProvider` |

A new store goes in its own `NativeOrchestrator.Data.{Provider}` project, which references Core only and uses
whatever data access suits the database: EF Core isn't required. It must pass `SagaStoreContractTests`: derive a test
class from it, as the in-memory and SQL Server tests do.
`SagaRecord` and `StepRecord` have private setters, because only the engine changes them. A store that doesn't use EF
Core rebuilds them from its rows with `SagaRecord.Restore(...)` and `StepRecord.Restore(...)`, and calls
`saga.IncrementVersion()` after a successful save. `saga.RequestCancellation(nowUtc)` applies the cancellation rules
for a store that changes sagas in memory.

## SQL Server store

Three tables in the `saga` schema: `Sagas` (one row per saga, with the lease and an `int` version), `SagaSteps` and
`SagaHistory` (append-only). A unique index on `(SagaType, CorrelationId)` makes starts idempotent, and a filtered
unique index on `ActiveInstanceKey` (set while a single-instance saga is active or failed) allows one at a time. An index on
`(Status, NextRunAtUtc)` serves the claim query.

```sh
dotnet tool restore
dotnet ef migrations add <Name> --project src/NativeOrchestrator.Data.SqlServer   # after changing the model
dotnet ef migrations script --idempotent --project src/NativeOrchestrator.Data.SqlServer -o saga.sql   # for deployment
```

`ApplyMigrationsOnStartup` is for development. In production, apply the script or a `dotnet ef migrations bundle` in
the deployment. `AddHealthChecks().AddSqlServerSagaStore()` checks that the database can be reached. By default the
store claims with `UPDLOCK, READPAST`, so instances never claim the same saga.
`UseLockingClaim: false` uses the provider-neutral `ExecuteUpdate` claim. Under contention, that claim can hand a saga
to two instances; the lease token on save rejects the second.

## Resilience

There are two layers:

| Layer | Handles | Built on |
| --- | --- | --- |
| In-call (seconds) | Timeouts, short retries, circuit breaker on each HTTP call | `Microsoft.Extensions.Http.Resilience`: `AddStandardResilienceHandler()` on each named client, configured from `Resilience` |
| Saga (minutes to days) | Retrying a failed step later, polling, timeouts, compensation | The engine (`IRetryPolicy`, `IBackoffStrategy`), saved in the database |

EF Core retries transient SQL errors on its own (`MaxRetryCount`).

Microsoft's resilience package is built on Polly (`Polly.Core` 8.4.2, transitively). `Directory.Build.targets` fails the
build if a project references Polly directly, or if a Polly version above `MaxAllowedPollyVersion` (8.4.2, in
`Directory.Build.props`) is resolved. Review the licence before raising it.

## Observability

- **Logs:** source-generated (`SagaLog`), with saga ID, type and step.
- **Traces:** an `ActivitySource` named `SagaOrchestrator`, one span per run.
- **Metrics:** a `Meter` named `SagaOrchestrator` with `sagaorchestrator.steps`, `sagaorchestrator.step.duration`
  and `sagaorchestrator.sagas.finished`.
- **History:** a per-saga timeline, exposed at `GET /sagas/{id}/history`.

## Development

```sh
dotnet build NativeOrchestrator.slnx
dotnet test --solution NativeOrchestrator.slnx        # SQL Server tests need Docker; without it they're skipped
dotnet format whitespace NativeOrchestrator.slnx --verify-no-changes
```

Package versions are central (`Directory.Packages.props`). Warnings are errors.

## Limits and next steps

- Steps run in sequence; parallel branches and waiting for external events (webhooks) aren't built yet.
- Polling checks stay in history only as counts; the first wait is recorded, not each check.
- Finished sagas aren't archived or deleted yet: add a retention job for `Sagas` and `SagaHistory`. Schedules add a
  saga row per occurrence.
- Renaming or removing a step breaks compensation of sagas already past it; version saga definitions before changing
  them.
