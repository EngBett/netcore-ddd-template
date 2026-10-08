# Agent instructions — netcore-ddd-template

Concise guidance for AI assistants and automation working in this repository.

## What this repo is

A **.NET 10** solution template using **DDD**, **Clean Architecture**, **CQRS (Wolverine)**, **EF Core** (SQL Server, PostgreSQL, SQLite, or MySQL), **Redis** caching, **Wolverine + RabbitMQ**, JWT, Serilog, Prometheus, and optional API styles (MVC, Minimal APIs, FastEndpoints). Projects are instantiated with `dotnet new ddd-template` (see `.template.config/template.json`).

**Wolverine is the only dispatcher.** It replaced MediatR (in-process CQRS) *and* MassTransit (RabbitMQ), both of which moved to paid licences. WolverineFx is MIT. Do not reintroduce `MediatR`, `MassTransit` 9.x, or add a second mediator library alongside Wolverine.

## Architecture rules

- **Dependency direction**: `Template.Api` → `Template.Application` → `Template.Domain`; `Template.Infrastructure` implements interfaces from `Application`; `Domain` has no infrastructure or framework references.
- **CQRS**: New features use **commands/queries** + **handlers** in `Template.Application`, validation via **FluentValidation**, optional domain events on entities in `Template.Domain`.
- **No marker interfaces.** Commands, queries and broker contracts are plain classes. A handler is any class named `<Something>Handler`/`<Something>Consumer` with a `Handle`/`Consume` method whose first parameter is the message; dependencies are injected into the constructor or the method. Do not add `IRequest<T>`-style markers — Wolverine does not use them.
- **Dispatch**: `IMessageBus.InvokeAsync<TResponse>(message)` runs a handler inline and returns its result (the MediatR `Send` equivalent). The response type is stated at the call site because there is no `IRequest<T>` to infer it from.
- **In-process vs broker**: whether a message crosses RabbitMQ is decided **only** by the `BrokerContracts` list in `src/Template.Application/DependencyInjection.cs`. Anything absent stays on an in-process local queue. Adding a broker contract means adding it there, which wires its routing *and* its retry policy together.
- **Data access**: Handlers depend on **`IApplicationContext`** (not concrete `DbContext` types from Application).
- **Response codes**: `ApiResponse.Code` becomes the HTTP status via `BaseController.CustomResponse` (controllers) and `ApiResponseResults.ToHttpResult()` (Minimal API / FastEndpoints). Keep the two switches identical.
- **Validation surfacing**: `UseFluentValidation()` throws `FluentValidation.ValidationException` (unwrapped) out of `InvokeAsync`. Client errors — that, `DomainException` (and subclasses), and unique-constraint violations — are mapped to a `400` by **`ClientErrorResponse.TryMap`**, applied in **two** places because `GlobalExceptionFilter` is an MVC filter and only covers controllers: the filter for the controllers style, and `ExceptionResponseMiddleware` for Minimal API / FastEndpoints. Everything else is a server fault: both callers build its `500` with **`ServerErrorResponse.Create`**, which logs the exception under the error code it puts in the message. Add a new mapping to `ClientErrorResponse` or `ServerErrorResponse`, never to one caller, or the styles diverge — `HttpResponses.feature` checks they don't. The middleware rethrows only when the response has started or the client aborted. Do not swap it for `UseExceptionHandler` with an empty fallback, which makes every declined exception return a 404 and then throw.
- **Domain events**: `Template.Domain` defines its own `IDomainEvent` marker and references **no** messaging package — keep it that way; that is the dependency rule. `DomainEventDispatcher.DispatchDomainEventsAsync` uses `InvokeAsync`, which dispatches on the runtime type and runs handlers inline (MediatR's `Publish` semantics). It **must** keep the `PreviewSubscriptions(...).Count == 0` guard: Wolverine throws `IndeterminateRoutesException` for a message nobody handles, so an event without a handler would otherwise fail the whole `SaveChangesAsync` rather than just skipping a side effect.

## Layout

- Three top-level folders: **`src/`** (the service), **`aspire/`** (`Template.AppHost`, `Template.ServiceDefaults`), **`tests/`** (`Template.Tests`). `Template.sln` and `global.json` stay at the repository root.
- `aspire/Template.AppHost` is the **only** project allowed to reference Aspire **Hosting** packages. `aspire/Template.ServiceDefaults` is referenced by the service and must stay free of Aspire packages entirely.
- Every path in **`.template.config/template.json`** — both `rename` keys/values and `exclude` entries — must carry the `src/` prefix. Miss one and `dotnet new` emits the wrong files: e.g. two `Program.cs` variants, which fails to compile on duplicate top-level statements. Scaffold each `--apiStyle` after touching it.
- `.dockerignore` lives at the **repository root**, because Docker reads it only from the build-context root; next to the Dockerfile it is silently ignored. `global.json` ships in scaffolds (not in `template.json`'s `exclude`) because the Dockerfile copies it.
- `src/Template.Api/Dockerfile` builds from the **repository root** as context. Its `COPY` list must name **every** project file the API references — the four siblings under `src/` *and* `aspire/Template.ServiceDefaults` — because `dotnet restore` fails on a missing `ProjectReference` target.

## Where to change behavior

| Concern | Primary location |
|--------|------------------|
| HTTP pipeline, Swagger, JWT middleware | `src/Template.Api/DependencyInjection.cs`, `Program.cs` |
| Wolverine (CQRS + broker), consumers | `src/Template.Application/DependencyInjection.cs` |
| EF Core, Redis, migrations | `src/Template.Infrastructure/DependencyInjection.cs` |
| Durable messaging (outbox, inbox, EF transactions) | `AddApplicationContext` / `AddDurableMessaging` in `src/Template.Infrastructure/DependencyInjection.cs`, `DurableMessagingOptions`, `Program` |
| Strongly typed app settings | `src/Template.Common/Options/*.cs` |
| Sample appsettings | `src/Template.Api/appsettings.json` and provider-specific variants |

## Aspire

- **The app host owns Aspire; the service knows nothing about it.** `Template.Api` must never reference an Aspire client package or call `AddServiceDiscovery`. The app host's only job is to translate resources into the **same explicit configuration keys the service already binds** (`DATABASE_CON`, `RedisOptions:*`, `RabbitMQOptions:*`, `ApplicationOptions:LogUrl`), using `WithEnvironment` and `__` for `:`. Anything injected must be settable in production as a plain env var with no Aspire present, and `dotnet run --project src/Template.Api` must keep working on its own.
- Add a new dependency by adding the resource in the app host and mapping it to a config key — not by adding a client package to the service.
- `AddProject` is called with a **path** (`"../Template.Api/Template.Api.csproj"`), not the generated `Projects.Template_Api`: `dotnet new` rewrites `Template` to the chosen project name and would turn that identifier into `Projects.Acme_Svc_Api`-style mush (`Projects.Acme.Svc_Api`), which does not compile.
- Each database branch names its database resource after its provider (`sqlserverdb`, `postgresdb`, `mysqldb`). Aspire rejects duplicate resource names, and the template source keeps **all** branches, so a shared name makes the template's own app host throw on startup.
- `DatabaseKind` is deliberately not injected; the provider's `appsettings.json` owns it.
- The app host's `UserSecretsId` is listed in `template.json`'s `guids` array so each scaffolded project gets a fresh one. Aspire stores the container passwords it generates there, and they must stay in step with the data volumes.
- To check the wiring without starting containers: `dotnet run --project aspire/Template.AppHost -- --publisher manifest --output-path manifest.json`. The manifest resolves the resource graph and every injected env var. `OTEL_EXPORTER_OTLP_ENDPOINT` is *not* in it — Aspire injects that at launch — so the OTLP path cannot be verified this way.
- **`ServiceDefaults` omits `AddServiceDiscovery()` on purpose.** Aspire's stock version includes it; here it would make the service's HTTP targets depend on how it was launched, which is exactly what the configuration rules forbid. OpenTelemetry, health checks and `AddStandardResilienceHandler` are kept because they are portable. Do not add it back to "match the template".
- ServiceDefaults registers the Wolverine `ActivitySource` and meter (both named `Wolverine`). Without them, message and command handling — most of what this service does — is absent from traces.
- `MapDefaultEndpoints()` maps `/alive` (liveness, `live`-tagged checks only). `/_health` stays the readiness endpoint mapped by `Template.Api`'s own middleware; do not merge them.

## Configuration conventions

- Bind options with **`IOptions<T>`** / **`Configure<T>(section)`** using section names that match the options class (e.g. `RedisOptions`, `MessagingOptions`, `RabbitMQOptions`, `ApplicationOptions`).
- **Redis**: `RedisOptions` (`ConnectionString`, `InstanceName`); wired in Infrastructure via `RedisCacheOptionsConfigurator` + `AddStackExchangeRedisCache`.
- **Messaging**: `RabbitMQOptions` is applied to Wolverine's **`ConnectionFactory`** (`UseRabbitMq(factory => ...)`), *not* turned into a URI — Wolverine's `Uri` overload only accepts an **`amqp://`** scheme and rejects MassTransit's old `rabbitmq://`. `MessagingOptions` (immediate retries, scheduled redelivery) maps onto one chained rule per broker handler chain: `chain.OnAnyException().RetryWithCooldown(...)` then `.Then.ScheduleRetry(...)` — applied by `BrokerResiliencePolicy`, not globally (see below).
- `Program` ends with `return await app.RunJasperFxCommands(args);` rather than `app.Run()`: the default still runs the web host, and Wolverine's operational commands (`resources setup`, `codegen`) become available.
- `Template.Tests` sets `DurableMessagingOptions__Enabled=false` as an **environment variable** in `ApiHost`. Durable messaging is wired while `Program` is still registering services, and `Program` re-adds `appsettings.json` after the host's own settings, so neither `UseSetting` nor `ConfigureAppConfiguration` can override it in time.
- Wolverine config lives in `AddWolverine(...)`, which runs before a provider exists, so read it from **`IConfiguration`** directly rather than `IOptions<T>`.
- **Retries are scoped, never global.** `opts.Policies.OnAnyException()` applies to `InvokeAsync` too, so a global retry policy makes a failed CQRS command retry on every cooldown before the caller sees the error — a validation failure becomes a multi-second hang instead of an immediate 400. `BrokerResiliencePolicy` (an `IHandlerPolicy`) applies the rules only to the broker contracts' handler chains, which is where MassTransit's receive-endpoint retries lived.
- **`ServiceLocationPolicy.AlwaysAllowed` is required.** Wolverine's codegen inlines dependency construction and by default rejects container lookups. Infrastructure aliases `IApplicationContext` to the `ApplicationContext` instance via a lambda, which Wolverine cannot see through, so every handler taking `IApplicationContext` would fail with `InvalidServiceLocationException`. Registering the interface against the concrete type instead would hand out a second `DbContext` per scope — a quieter and worse bug.
- **Validators are registered once, by `UseFluentValidation()`.** It defaults to `DiscoverAndRegisterValidators`; adding `AddValidatorsFromAssembly` as well registers each validator twice and every rule then runs twice.
- **`WolverineFx.RuntimeCompilation`** is a required package, not an optional extra: core WolverineFx 6.x dropped the Roslyn runtime compiler and the host throws on startup without it.
- Do **not** reintroduce a flat `"Redis"` string key; use **`RedisOptions`** in JSON.

## Durable messaging

`DurableMessagingOptions` (`Enabled`, `SchemaName`, `AutoBuildStorage`) turns on Wolverine's transactional outbox and inbox in the service's own database. It is **on** in the shipped PostgreSQL and SQL Server `appsettings`, **off** for SQLite and MySQL (Wolverine's EF Core integration covers only the first two), and off by default in code so a bare configuration behaves as it did before.

When on, a handler that depends on `IApplicationContext` runs in one database transaction that also stores the messages it publishes and the domain events its entities raised. A commit and its messages stand or fall together; a crash after the commit cannot lose them.

- **Two registrations, both required.** `AddApplicationContext<TContext>` (Infrastructure, via `AddInfrastructureDependencies`) registers the `DbContext` with Wolverine integration. `opts.AddDurableMessaging<TContext>(configuration)` registers the persistence and the transaction middleware, and must run **inside `AddWolverine`**: Wolverine forbids extensions registered in the container (`ConfigureWolverine`) from changing service registrations. Application cannot reference Infrastructure, so `AddApplicationDependencies` takes a `configureWolverine` callback and `Program` passes it. `DurableMessagingGuard` fails the host at startup if the first is on and the second was forgotten.
- **`WithDbContextAbstraction<IApplicationContext, TContext>()` is what makes this work.** Wolverine finds the `DbContext` for a handler by looking at its dependencies and cannot see through an interface, so without the mapping the transaction middleware silently does not apply (not even with `[Transactional]`): `SaveChangesAsync` commits on its own, a handler that throws afterwards leaves the row behind, and its messages are not in the outbox. It also makes `ServiceLocationPolicy.AlwaysAllowed` unnecessary for this path.
- **Domain events** are published, not scraped. With durable messaging on, `ApplicationContext.SaveChangesAsync` saves, then `PublishAsync`es each tracked entity's events (and clears them), instead of invoking their handlers inline (which is still what it does when durable messaging is off). Inside a handler's transaction that writes them to the outbox in the same commit. Consequence: event handlers run **after** the commit, asynchronously, **at least once**, so they must be idempotent. An event with no handler is simply not delivered. **Do not switch this to `PublishDomainEventsFromEntityFrameworkCore`**: in testing, events enqueued by that scraper were handled but never written to the inbox, so a restart lost them, while a plain `PublishAsync` was stored and survived (`An_event_committed_but_not_yet_handled_is_handled_by_the_next_host` is the test that tells them apart). Publishing only reaches the outbox inside a Wolverine handler; a write made outside one publishes immediately and non-durably, and in this template all writes go through handlers.
- **Sagas** use EF Core storage when the saga type is mapped in the `DbContext`. A saga must implement `JasperFx.IRevisioned` (the `Saga` base class has `Version` but not the interface) and map `Version` as a concurrency token: put an `IEntityTypeConfiguration<TSaga>` in `DataAccess/EntityConfigurations` (`OnModelCreating` applies every one in the assembly) and call `modelBuilder.ConfigureSaga<TSaga>()` from it, which will not compile without `IRevisioned`. Without both, concurrent updates are silently lost: in testing, eight concurrent updates all reported success and only two were applied. With both, the losers throw `SagaConcurrencyException`. Give every saga a static `NotFound`; without one a message for a missing saga throws. `tests/Template.DurableTests/Orders.cs` is the reference saga (start, update, timeout, `NotFound`) and `SagaTests.cs` its tests. It lives in the test project, not `Template.Application`, because Wolverine discovers saga types by convention in every scaffold, and SQLite and MySQL scaffolds have no saga storage.
- **Schema.** Wolverine keeps its tables in `SchemaName` (default `wolverine`): envelope, node, agent and dead-letter tables. EF migrations cannot own all of them. `AutoBuildStorage` lets the host create them at startup, which needs DDL rights, so it is a **Development-only** convenience and is a hard failure elsewhere (same stance as `EnableAutoMigration`). Elsewhere run `dotnet run --project src/Template.Api -- resources setup` as a deliberate step with a role that can create schema (and set `EnableAutoMigration` to false for that run). An application role then needs only `SELECT, INSERT, UPDATE, DELETE` on the application and `wolverine` schemas. If the schema is missing the host refuses to start and says so.
- **Broker down.** A broker outage while running is absorbed: messages wait in the outbox. A broker that is down at **startup** stops the host (`AutoProvision()` throws `BrokerInitializationException`), so readiness and orchestration must expect that.
- **Recovery is not instant.** Envelopes owned by a node that stopped or died are taken over by another node after Wolverine's node-staleness window: about 34 s after a crash and about 40 s after a graceful stop, in testing. A payout event can sit that long.
- **Inbox rows** for handled messages are retained as the dedupe record; do not treat their presence as a backlog.

## Tests

- `tests/Template.DurableTests` (PostgreSQL scaffolds only) is plain xUnit against a real PostgreSQL started by **Testcontainers**, so it **needs Docker**. It is deliberately **not** in `Template.sln`, which keeps a plain `dotnet test` Docker-free; run it with `dotnet test tests/Template.DurableTests`. It covers rollback of a handler that throws after `SaveChangesAsync`, exactly-once event handling, an event with no handler, an in-flight event surviving a host restart (slow: about 45 s), sagas (storage, `NotFound`, lost-update protection, timeout), refusing to start when the schema is missing, running as a role with no DDL rights, and the startup guard for a forgotten `AddDurableMessaging`. Break `WithDbContextAbstraction` and three fail.
- `tests/Template.Tests` is a **Reqnroll** (Gherkin) suite on xUnit. Features in `Features/`, bindings in `Steps/`, fixtures in `Support/`.
- `Support/TestHost.cs` boots the service's **real** `AddApplicationDependencies`. Substitute nothing beyond the two things it already does: `DisableAllExternalWolverineTransports()` and SQLite in memory. `dotnet test` must keep needing no Docker.
- `Support/ApiHost.cs` boots the API's **real** `Program` via `WebApplicationFactory`, with the same two substitutions plus auto-migration off and the `QueryFaults` middleware on `GetTodosQuery`. HTTP scenarios must stay style-agnostic: they run unchanged in a controllers, Minimal API or FastEndpoints scaffold, so use only the shared `/api/v1/test` route and `ApiResponse` body.
- Wolverine compiles a handler the first time its message is dispatched (~1 s). A scenario that times a dispatch must handle one of that message first, or it fails whenever xUnit orders it first.
- Assert **behaviour through public APIs**, not Wolverine internals. Retry scoping is covered by asserting a rejected command returns in under 250 ms, not by inspecting handler chains — apply the policy globally and that scenario fails (the run went from 0.8 s to 8 s when tried).
- `Support/SpyHandlers.cs` adds a second handler for `TodoCreatedEvent` so dispatch is observable; it is found because `TestHost` adds the test assembly to Wolverine's discovery. Reset its counter in a `[BeforeScenario]`.
- Do **not** add FluentAssertions: version 8 moved to a paid licence, which would undo the reason this template dropped MediatR and MassTransit. Use xUnit's `Assert`.
- After changing anything in `Template.Application` or `Template.Infrastructure`, run `dotnet test`.

## SDK

- `global.json` pins **`10.0.100`** with **`rollForward: "latestFeature"`** so any installed .NET 10 SDK in the feature band can build. If CI requires an exact patch, align `global.json` or install that SDK.

## Template authoring

- When adding symbols or conditional files, update **`.template.config/template.json`** and any **`README.md`** installation or configuration docs that describe template flags.

## Style for edits

- Match existing naming, file layout, and comment density in touched files.
- Scope changes to the requested behavior; avoid unrelated refactors or new markdown unless the user asks.
- After substantive C# changes, run **`dotnet build`** from the repo root (network may be needed for restore).

For human-oriented documentation, prefer **`README.md`**.
