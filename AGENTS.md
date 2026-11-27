# AGENTS.md

Guidance for engineers and coding agents working on NativeOrchestrator. Read it before changing code. The
[README](README.md) explains how to use the library and how it works. This file records **why** it is built this way,
the rules that keep it correct, and what is left to do.

## What this is

A central orchestrator service for **long-running sagas**. It calls external systems, polls them until their work
finishes, and compensates the steps that succeeded when a later one fails. It is built on the .NET Generic Host, with no
workflow engine (no Durable Task, MassTransit or Temporal).

### Requirements it was built to

| #   | Requirement                                                                 | How it is met                                                                                                                                                                                                                                                                                                                           |
| --- | --------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Prioritise built-in .NET capabilities                                       | Generic Host, `BackgroundService`, options + validation, DI, `TimeProvider`, System.Text.Json source generation, `IHttpClientFactory`, `[LoggerMessage]`, `Meter`/`ActivitySource`, health checks, EF Core, `Microsoft.Extensions.Http.Resilience`, central package management. Third-party code only in tests (xUnit, Testcontainers). |
| 2   | Saga storage that doesn't depend on the database; implemented on SQL Server | The engine depends only on `ISagaRepository` + `ISagaWorkQueue`. `NativeOrchestrator.EntityFrameworkCore` implements them for any relational provider; `NativeOrchestrator.SqlServer` adds the provider, migrations and a locking claim.                                                                                                |
| 3   | Sagas longer than 12 hours                                                  | Nothing waits in memory. Each saga is a database row with a due time (`NextRunAtUtc`); polls, retries and timeouts are stored, so they survive restarts. Tested to 26 hours in-memory and 14 hours on SQL Server, on a fake clock.                                                                                                      |
| 4   | Polling to see whether an activity has finished                             | `PollingSagaStep<TData, TOperation>` (start once, then check) and `HttpPollingSagaStep<TData>` (HTTP `202` + `Location`, honouring `Retry-After`), with `PollingOptions` backoff and a step timeout.                                                                                                                                    |
| 5   | SOLID and the options pattern                                               | Small single-purpose classes (claim, run steps, compensate, keep the lease, store). Extension through new classes and interfaces, not edits. Every setting is an options class, bound from configuration and validated at startup.                                                                                                      |
| 6   | Base classes with many concrete classes                                     | `Saga<TData>`, `SagaStep<TData>`, `PollingSagaStep`, `HttpSagaStep`, `HttpPollingSagaStep`. The app has two sagas and six steps built on them.                                                                                                                                                                                          |
| 7   | Customisable                                                                | Hooks on `Saga<TData>`; replaceable `IRetryPolicy`, `IBackoffStrategy`, `ISagaSerializer`, `ISagaClaimStrategy`, store and `TimeProvider`; `ISagaLifecycleObserver`; options per saga and per step.                                                                                                                                     |
| 8   | Code in a folder called `NativeOrchestrator`                                | This folder, with its own solution. It is self-contained so it can move to its own repository.                                                                                                                                                                                                                                          |
| 9   | Base classes in a separate class library from the app                       | `NativeOrchestrator.Core` (plus the two store libraries) is separate from `NativeOrchestrator.App`.                                                                                                                                                                                                                                     |
| —   | Resilience with Microsoft's package, not Polly (Polly's licensing change)   | `AddStandardResilienceHandler()` from `Microsoft.Extensions.Http.Resilience`. A build guard caps Polly (see [Decisions](#decisions)).                                                                                                                                                                                                   |

## Decisions

Each line is a deliberate choice. Change one only on purpose, and update this table.

| Area                  | Decision                                                                                                                                                                                                                                                                                          | Why                                                                                                                                                                                                           |
| --------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Solution              | Separate `NativeOrchestrator.slnx`; central package versions; warnings as errors; code style enforced in the build                                                                                                                                                                                | Independent of the Durable Functions pilot it started next to.                                                                                                                                                |
| Hosting               | One ASP.NET Core process runs the HTTP API and the worker. `WorkerEnabled: false` turns the worker off.                                                                                                                                                                                           | Simplest to deploy. Any number of instances share the database.                                                                                                                                               |
| Triggers              | HTTP (`POST /sagas/{type}`) and CRON schedules declared at setup (`AddSchedule`).                                                                                                                                                                                                                 | Agreed scope. Queue triggers are a possible follow-up.                                                                                                                                                        |
| Persistence           | EF Core; one short-lived `DbContext` per operation through `IDbContextFactory`                                                                                                                                                                                                                    | Database-agnostic, and no connection stays open while a saga waits.                                                                                                                                           |
| Persistence model     | `SagaRecord`/`StepRecord` live in Core. Leases are EF **shadow properties**, so Core's model has no store details. Enums are stored as strings; data and step state as JSON text.                                                                                                                 | Core stays free of persistence concerns. Rows are readable in SQL. JSON text works on every provider.                                                                                                         |
| Schema                | Fixed `saga` schema; tables `Sagas`, `SagaSteps`, `SagaHistory`; unique `(SagaType, CorrelationId)` (filtered for nulls on SQL Server); index `(Status, NextRunAtUtc)`                                                                                                                            | A configurable schema conflicts with generated migrations.                                                                                                                                                    |
| Single instance       | Opt-in `SingleInstance` (on for `report-generation`). The client sets `SagaRecord.ActiveInstanceKey` = saga type, unique where set. `SagaRun.Finish` clears it, but keeps it on `Failed` while `FailedBlocksNewStarts` (default on). A blocked start gets the holder as `AlreadyRunning`.         | The database enforces it across instances and racing starts. A failed saga needs manual action, so by default the next one waits until an operator clears the key.                                            |
| Scheduling            | Stateless: each instance computes occurrences (own parser in Core) and starts the latest due one with correlation ID `schedule:{name}:{time}`. Missed ones start only within `MisfireThreshold` (1 h); one blocked by `SingleInstance` is skipped.                                                | The correlation index already gives exactly-once starts across instances, so no table or lease. A parser in Core keeps runtime code free of third-party packages.                                             |
| Execution model       | A saga advances in **runs**: claim → run steps until one must wait → save → release. It is saved after every step (checkpoint).                                                                                                                                                                   | Long sagas without memory or threads held. A crash repeats at most one step.                                                                                                                                  |
| Delivery              | Steps run **at least once**. `context.IdempotencyKey` = `{sagaId:N}:{step}` (`:compensate` for compensation), stable across retries. The HTTP base classes send it as `Idempotency-Key`.                                                                                                          | Exactly-once isn't possible with external systems. The key lets them ignore repeats. (The first plan included the attempt number; it was dropped, because a key that changes on retry defeats deduplication.) |
| Concurrency           | A **lease** (owner, token, expiry) for mutual exclusion, plus an `int Version` concurrency token on every save. A save needs the current token _and_ the version it read.                                                                                                                         | The lease stops two instances running the same saga. The version stops a stale save overwriting newer state.                                                                                                  |
| Lease renewal         | `LeaseKeeper` renews every `LeaseRenewalInterval`. If the lease is lost, it cancels the run token.                                                                                                                                                                                                | Steps can take longer than a lease.                                                                                                                                                                           |
| Cancellation          | `RequestCancellationAsync` sets the flag, makes the saga due now **and bumps the version**, so a run in progress fails its next save and drops its work. The next run compensates.                                                                                                                | No lost cancellations, at the cost of possibly repeating one step (which is allowed).                                                                                                                         |
| Claiming              | SQL Server default: CTE with `UPDLOCK, READPAST, ROWLOCK`. Provider-neutral fallback: `ExecuteUpdate ... WHERE Id IN (SELECT TOP n ...)`, which can double-claim under contention; the save's token check makes that safe.                                                                        | Many instances claim without blocking each other. Other providers still work.                                                                                                                                 |
| Failures              | An exception = transient (durable retry through `IRetryPolicy`). `StepResult.Failure` / `StepFailedException` = permanent. HTTP: `408`, `429`, `5xx` transient; other `4xx` permanent. A failed step compensates unless it has `ContinueOnError`.                                                 | A clear, predictable split.                                                                                                                                                                                   |
| Compensation          | Only steps that **succeeded** are compensated, latest first. A failed compensation is retried (`CompensationRetry`), then marked `CompensationFailed`. The others still run, and the saga ends `Failed` (manual action).                                                                          | The classic saga model. A broken compensation is never retried forever.                                                                                                                                       |
| Timeouts              | Step timeout measured from the step's **first** start; saga `MaxDuration` from the saga's start; both from stored times. A wait never goes past the step deadline. `ExecutionTimeout` bounds one call.                                                                                            | Restarts don't reset timeouts, and timeouts are detected on time.                                                                                                                                             |
| Polling               | `CheckCount` drives backoff. `Retry-After` is honoured but capped at `MaxDelay`. History records only the first wait (`StepWaiting`), not each check.                                                                                                                                             | Bounded history for sagas that poll for days.                                                                                                                                                                 |
| Options               | Precedence: class defaults → `SagaDefaults` → the saga's code → `Sagas:{name}`. Steps: configuration → code → saga. Per-step values are nullable so the layers merge. Validated at startup (`ValidateOnStart`, `IValidateOptions`); invalid step options in code fail when the registry is built. | Operations can tune timeouts without a deployment, and bad configuration fails fast.                                                                                                                          |
| Registration          | Saga and step names are explicit or default to the class name without `Saga`/`Step`. Duplicates and empty sagas fail at startup: the worker depends on `SagaRegistry`, which builds every saga.                                                                                                   | Names are stored in rows and used as configuration keys.                                                                                                                                                      |
| Unknown saga type     | A claimed saga whose type isn't registered is put off by `UnknownSagaTypeRetryDelay`, not failed.                                                                                                                                                                                                 | Safe during rolling deployments.                                                                                                                                                                              |
| Engine errors         | An unexpected engine or store error keeps the lease, so the saga runs again once the lease expires. A concurrency conflict or shutdown releases it at once.                                                                                                                                       | A natural backoff for repeated failures; fast recovery otherwise.                                                                                                                                             |
| Final save            | Not cancelled by shutdown. The store's command timeout bounds it.                                                                                                                                                                                                                                 | Finished work isn't thrown away on shutdown.                                                                                                                                                                  |
| Hooks and observers   | `OnCompletedAsync`/`OnFailedAsync` and `ISagaLifecycleObserver` run **after** the save, at most once; exceptions are logged.                                                                                                                                                                      | Notifications never block or corrupt saga state. For guaranteed delivery, read `SagaHistory` (or add an outbox).                                                                                              |
| HTTP resilience       | Two layers: Microsoft's standard resilience handler per named client (seconds), and the engine's durable retries (minutes to days). POST is retried too, which is safe thanks to idempotency keys.                                                                                                | Short blips don't cost a saga retry; long outages don't hold calls open.                                                                                                                                      |
| Polly                 | No direct Polly references. `Directory.Build.targets` fails the build on a direct reference or a transitive Polly above `MaxAllowedPollyVersion` (8.4.2, from `Microsoft.Extensions.Resilience` 11.0.1).                                                                                          | The user's licensing concern. Microsoft's package still depends on Polly.Core, so the guard makes any upgrade a conscious decision.                                                                           |
| Serialization         | System.Text.Json, web defaults, source-generated contexts first (`ConfigureSerializer`), reflection as fallback.                                                                                                                                                                                  | Fast and trimming-friendly.                                                                                                                                                                                   |
| Namespaces            | Flat: `NativeOrchestrator` (Core public API), `NativeOrchestrator.EntityFrameworkCore`, `NativeOrchestrator.SqlServer`; DI extensions in `Microsoft.Extensions.DependencyInjection` / `NativeOrchestrator`. Folders group by role.                                                                | One `using` for consumers.                                                                                                                                                                                    |
| Saga hooks visibility | `Configure`, `Validate`, `OnCompletedAsync`, `OnFailedAsync` are `protected`; the runtime calls them through `internal Invoke*` wrappers.                                                                                                                                                         | `protected internal` breaks overrides in assemblies that see internals (the tests).                                                                                                                           |
| Tests                 | xUnit v3 on Microsoft.Testing.Platform. Engine tests on `FakeTimeProvider` with no worker (`SagaTestHost` jumps the clock). Store contract tests shared by every store. SQL Server through **Testcontainers**, skipped with a reason when Docker isn't available.                                 | Fast, deterministic tests for multi-day sagas, and the real database for the store.                                                                                                                           |

## Layout

```
NativeOrchestrator.slnx
global.json                    # .NET 11 SDK; Microsoft.Testing.Platform for dotnet test
Directory.Build.props          # net11.0, nullable, warnings as errors, code style in build, XML docs for libraries
Directory.Build.targets        # Polly guard
Directory.Packages.props       # every package version
.config/dotnet-tools.json      # dotnet-ef
compose.yaml                   # local SQL Server
docs/index.html                # interactive docs (rule 12): principles, workflows, diagrams, dependencies, examples
src/
  NativeOrchestrator.Core/                  # no database or ASP.NET Core dependency
    Sagas/        base classes: Saga, SagaStep, PollingSagaStep, HttpSagaStep, HttpPollingSagaStep, SagaHttp
    Building/     ISagaBuilder, SagaBuilder, StepDefinition, EffectiveStepOptions, SagaRuntime, SagaRegistry
    Context/      StepContext, SagaContext
    Execution/    SagaWorker, SagaEngine, SagaStepExecutor, CompensationRunner, LeaseKeeper, StepInvoker, SagaRun, SagaWorkSignal
    Persistence/  ISagaRepository, ISagaWorkQueue, ISagaStoreInitializer, records, exceptions, serializer
    Policies/     IRetryPolicy, DefaultRetryPolicy, IBackoffStrategy, ExponentialBackoffStrategy
    Options/      NativeOrchestratorOptions, SagaOptions, StepOptions, RetryOptions, PollingOptions, validators
    Results/      StepResult, PollResult, statuses, SagaSnapshot, exceptions
    Client/       ISagaClient, SagaClient
    Scheduling/   CronExpression, SagaScheduler, SagaScheduleOptions, SagaScheduleRegistry, ScheduledOccurrence
    Observability/ SagaLog, SagaDiagnostics, ISagaLifecycleObserver
    Testing/      InMemorySagaStore
    DependencyInjection/  AddNativeOrchestrator, NativeOrchestratorBuilder
  NativeOrchestrator.EntityFrameworkCore/   # SagaDbContext, mapping (Model/), EntityFrameworkSagaStore, claim strategies
  NativeOrchestrator.SqlServer/             # UseSqlServer, SqlServerClaimStrategy, options, Migrations/
  NativeOrchestrator.App/                   # API, sagas, external service clients, simulated systems, observer
tests/
  Shared/                                   # SagaStoreContractTests, SagaTestHost, ScriptedSaga (linked into both projects)
  NativeOrchestrator.Core.Tests/
  NativeOrchestrator.SqlServer.Tests/       # Testcontainers
```

## Commands

Prerequisites: the .NET 11 SDK (`global.json`), and Docker for the SQL Server tests and for running the app.

```sh
dotnet build NativeOrchestrator.slnx
dotnet test --solution NativeOrchestrator.slnx
dotnet format whitespace NativeOrchestrator.slnx --verify-no-changes
docker compose up -d && dotnet run --project src/NativeOrchestrator.App       # http://localhost:5080
dotnet tool restore && dotnet ef migrations add <Name> --project src/NativeOrchestrator.SqlServer
```

**Before finishing any change:** the build passes (with no warnings, since warnings are errors), `dotnet test` passes
with the SQL Server tests run, not skipped, and the formatting check is clean. If you changed the model, add a
migration. If you changed behaviour, update the README. If the change touches anything the interactive docs cover,
update [`docs/index.html`](docs/index.html) in the same change (rule 12).

## Code style

`.editorconfig` encodes most of these rules, and the build enforces them:

- 2-space indentation everywhere, **including continuation lines** (+2, not +4; `dotnet format` doesn't fix these).
- LF line endings, UTF-8, final newline, file-scoped namespaces, braces on new lines.
- **No primary constructors** on classes or structs: a standard constructor that assigns `private readonly` fields.
  Positional records are fine.
- Private fields `_camelCase`, never `this.`. Private static fields and constants PascalCase.
- One-line guard clauses have no braces.
- One type per file (private nested types and test helpers excepted).
- Public members of the libraries have XML docs (`GenerateDocumentationFile`).
- Validate public arguments with `ArgumentNullException.ThrowIfNull` / `ArgumentException.ThrowIfNullOrWhiteSpace`.
- Logging goes through `[LoggerMessage]` methods (`SagaLog` in Core).
- Library code uses `ConfigureAwait(false)`.

## Rules that keep it correct

1. **Core has no database or web dependency.** Persistence goes through `ISagaRepository`/`ISagaWorkQueue`.
2. **Every store passes `SagaStoreContractTests`.** Change the contract (interface docs) and the contract tests together.
3. **Nothing waits in memory across a poll or retry.** A step that must wait returns `StepResult.Poll(...)` or throws; it
   never sleeps.
4. **Save after every step**, before the next one runs. Don't batch steps into one save.
5. **Every save is fenced** by lease token and version. Never add a write path that skips them (cancellation
   deliberately bumps the version).
6. **Steps must be idempotent** and send `context.IdempotencyKey`. Compensations too, since they can repeat.
7. **Use `TimeProvider`** (`context.TimeProvider`) for time. Never `DateTime.UtcNow`, or tests on the fake clock
   break.
8. **Saga and step names are persisted.** Renaming or reordering steps affects sagas that are running (see
   [Next steps](#next-steps)).
9. **No direct Polly.** Keep the guard. Raise `MaxAllowedPollyVersion` only after reviewing the licence.
10. **Fail at startup on bad configuration**, with a message that says how to fix it.
11. **Keep the public API small.** Implementation types are `internal`. Tests see internals through
    `InternalsVisibleTo`.
12. **Keep the interactive docs current.** [`docs/index.html`](docs/index.html) documents the design principles,
    workflows, activity diagrams, class hierarchy, dependency tree, features with examples, and configuration. Update
    it in the same change whenever one of those changes: a rule or decision, how a saga is started, run, cancelled,
    leased or scheduled, a status or step outcome, a base class or its members, a constructor dependency or a new
    internal service, a public API, an option or its default, or a feature. All content is in the `DOCS` data block at
    the end of the file (`principles`, `diagrams`, `workflows`, `activities`, `types`, `features`, `configuration`);
    diagrams are Mermaid text. Keep the examples compiling against the real API and in the repository's code style.
    After editing, open the page and check that every diagram renders (a parse error shows as "Diagram error").

## How to...

- **Add a saga:** a `Saga<TData>` subclass plus its steps; `AddSaga<TSaga, TData>("name")`; add `TData` (and any step
  state types) to the app's `JsonSerializerContext`; optional `Sagas:{name}` configuration; demo requests in the
  `.http` file.
- **Add a schedule:** `AddSchedule<TData>("name", "saga", "cron", occurrence => data)` after the saga's `AddSaga`;
  optional `Schedules:{name}` configuration. Keep the name stable: it is part of each occurrence's correlation ID.
- **Add a kind of step:** a base class in `Core/Sagas/` deriving from `SagaStep<TData>` (as `PollingSagaStep` does).
  Engine changes are only needed if it needs a new `StepResult` kind.
- **Add an option:** a property on the options class, a check in its validator, README configuration docs, tests for
  precedence and validation.
- **Add a store:** implement both interfaces (or use EF Core with another provider plus its own migrations project, as
  `NativeOrchestrator.SqlServer` does), then derive a test class from `SagaStoreContractTests`.
- **Change the model:** edit `Model/*Configuration.cs`, then add a migration in `NativeOrchestrator.SqlServer`.
- **Test engine behaviour:** use `SagaTestHost` with a `SagaShape` and `Script` (`tests/Shared`). Name tests as
  sentences (`A_failed_compensation_is_retried_then_...`), and cover the success path and the failure and edge cases.

## Next steps

Ordered by what to do first. Items marked **(gap)** are gaps in the current implementation, not new features.

### 1. Test gaps

- [ ] **(gap)** `ExecutionTimeout`: a hanging step is cancelled and retried (needs the fake clock advanced while the step waits).
- [ ] **(gap)** Lease loss mid-run: `LeaseKeeper` cancels the run and nothing is saved.
- [ ] **(gap)** An unknown saga type is put off by `UnknownSagaTypeRetryDelay`.
- [ ] **(gap)** `ISagaLifecycleObserver` receives every saved event once, and a failing observer doesn't affect the saga.
- [ ] **(gap)** App tests with `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) and a Testcontainers SQL
      Server: API status codes, a full run through the simulated systems, and options validation at startup.
- [ ] Chaos test: kill an instance during a step; another instance finishes the saga after the lease expires.
- [ ] Load test of claim throughput with several workers (for tuning `MaxConcurrency`, `ScanInterval` and indexes).

### 2. Before production

- [ ] **Authentication and authorization** on `/sagas` (for example, JWT bearer with policies per saga type). The API is open today.
- [ ] **Secrets:** the connection string from environment variables, user secrets or a key vault. `appsettings.Development.json`
      has the local Docker password only.
- [ ] **Real external systems:** replace the `ExternalServices` URLs, set `Simulated:Enabled` to `false`, and remove
      the `Simulated/` folder when it is no longer needed.
- [ ] **Migrations in the deployment** (`dotnet ef migrations bundle` or an idempotent script); keep `ApplyMigrationsOnStartup` off.
- [ ] **OpenTelemetry:** export the `NativeOrchestrator` activity source and meter, plus ASP.NET Core, HttpClient and EF Core.
- [ ] **Liveness and readiness** health checks (database readiness separate from process liveness).
- [ ] **Operations endpoints:** list and filter sagas (`GET /sagas?status=Failed&type=...`, paged); retry a failed
      compensation; resume or skip a step for a saga that needs manual action; release a failed single-instance saga
      (clear its `ActiveInstanceKey`, today done in SQL).
- [ ] **Retention:** a background job that deletes or archives finished sagas and their history after N days.
- [ ] **Alerts** on `Failed` sagas (observer or query), on a growing number of due sagas (queue lag), and on lease losses.
- [ ] Rate limiting on the start endpoint (`Microsoft.AspNetCore.RateLimiting`).

### 3. Engine features

- [ ] **External events / callbacks:** a step that waits for a webhook (`POST /sagas/{id}/events/{name}`) instead of
      polling, with a timeout.
- [ ] **Parallel steps** (fan-out/fan-in), with results applied in declaration order.
- [ ] **Saga definition versioning:** store a definition version per saga, so steps can be renamed, added or removed
      without breaking sagas that are running.
- [ ] **Cleaning up the failed step:** let a polling step cancel its external operation when it times out or the saga
      is cancelled (today only succeeded steps are compensated).
- [ ] Scheduled starts (`startAtUtc`) and priorities in the claim order.
- [ ] Optional transactional **outbox** for lifecycle events (today they are at most once).
- [ ] Optionally record every poll in history, or poll-progress details from the external system.
- [ ] A saga that starts other sagas and waits for them (sub-sagas).
- [ ] Queue triggers (for example, Azure Service Bus) as an alternative to HTTP.

### 4. Store

- [ ] A second provider (PostgreSQL) to prove the abstraction, with its own migrations project and a locking claim
      (`FOR UPDATE SKIP LOCKED`).
- [ ] Review `DateTimeOffset` handling on providers without native support (SQLite orders it as text).
- [ ] Index and partitioning review once real volumes are known; consider compressing large `DataJson`.

### 5. Packaging (if the libraries become NuGet packages)

- [ ] Package metadata (`PackageId`, version, authors, repository URL), Source Link, symbols.
- [ ] `Microsoft.CodeAnalysis.PublicApiAnalyzers` to track public API changes.
- [ ] Decide whether `InMemorySagaStore` and `tests/Shared` (contract tests, `SagaTestHost`) ship as a
      `NativeOrchestrator.Testing` package for consumers.

## Known limitations

- The provider-neutral claim can hand one saga to two instances under contention. It is safe (the second save is
  rejected) but repeats a step. Use the locking claim where the provider supports one.
- Lifecycle events and saga hooks are at most once.
- History and finished sagas grow without limit until retention exists. Schedules add one saga per occurrence.
- A scheduled occurrence skipped because a single-instance saga was active isn't recorded, so an instance that
  starts within the misfire threshold may still start it late.
- In a time zone with daylight saving, a schedule doesn't run during the repeated hour after its first pass.
- Polly.Core 8.4.2 is present transitively through Microsoft's resilience packages.
