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
- **Validation surfacing**: `UseFluentValidation()` throws `FluentValidation.ValidationException` (unwrapped) out of `InvokeAsync`. It is mapped to a `400` in **two** places because `GlobalExceptionFilter` is an MVC filter and only covers controllers: the filter for the controllers style, and `ValidationExceptionMiddleware` for Minimal API / FastEndpoints. Both use `ValidationFailureResponse.From` so the payload is identical. The middleware rethrows anything that is not a validation failure — do not widen it to a general exception handler, and do not swap it for `UseExceptionHandler` with an empty fallback, which makes every other exception return a 404 and then throw.
- **Domain events**: `Template.Domain` defines its own `IDomainEvent` marker and references **no** messaging package — keep it that way; that is the dependency rule. `DomainEventDispatcher.DispatchDomainEventsAsync` uses `InvokeAsync`, which dispatches on the runtime type and runs handlers inline (MediatR's `Publish` semantics). It **must** keep the `PreviewSubscriptions(...).Count == 0` guard: Wolverine throws `IndeterminateRoutesException` for a message nobody handles, so an event without a handler would otherwise fail the whole `SaveChangesAsync` rather than just skipping a side effect.

## Layout

- Projects live under **`src/`**; **`tests/`** sits beside it for test projects (currently a placeholder holding only `.gitkeep`). `Template.sln` and `global.json` stay at the repository root.
- `src/Template.AppHost` is the .NET Aspire app host. It is the **only** project allowed to reference Aspire packages.
- Every path in **`.template.config/template.json`** — both `rename` keys/values and `exclude` entries — must carry the `src/` prefix. Miss one and `dotnet new` emits the wrong files: e.g. two `Program.cs` variants, which fails to compile on duplicate top-level statements. Scaffold each `--apiStyle` after touching it.
- `src/Template.Api/Dockerfile` builds from the **repository root** as context, so its `COPY`/`restore` paths are `src/Template.Api/...`.

## Where to change behavior

| Concern | Primary location |
|--------|------------------|
| HTTP pipeline, Swagger, JWT middleware | `src/Template.Api/DependencyInjection.cs`, `Program.cs` |
| Wolverine (CQRS + broker), consumers | `src/Template.Application/DependencyInjection.cs` |
| EF Core, Redis, migrations | `src/Template.Infrastructure/DependencyInjection.cs` |
| Strongly typed app settings | `src/Template.Common/Options/*.cs` |
| Sample appsettings | `src/Template.Api/appsettings.json` and provider-specific variants |

## Aspire

- **The app host owns Aspire; the service knows nothing about it.** `Template.Api` must never reference an Aspire client package or call `AddServiceDiscovery`. The app host's only job is to translate resources into the **same explicit configuration keys the service already binds** (`DATABASE_CON`, `RedisOptions:*`, `RabbitMQOptions:*`, `ApplicationOptions:LogUrl`), using `WithEnvironment` and `__` for `:`. Anything injected must be settable in production as a plain env var with no Aspire present, and `dotnet run --project src/Template.Api` must keep working on its own.
- Add a new dependency by adding the resource in the app host and mapping it to a config key — not by adding a client package to the service.
- `AddProject` is called with a **path** (`"../Template.Api/Template.Api.csproj"`), not the generated `Projects.Template_Api`: `dotnet new` rewrites `Template` to the chosen project name and would turn that identifier into `Projects.Acme_Svc_Api`-style mush (`Projects.Acme.Svc_Api`), which does not compile.
- Each database branch names its database resource after its provider (`sqlserverdb`, `postgresdb`, `mysqldb`). Aspire rejects duplicate resource names, and the template source keeps **all** branches, so a shared name makes the template's own app host throw on startup.
- `DatabaseKind` is deliberately not injected; the provider's `appsettings.json` owns it.
- The app host's `UserSecretsId` is listed in `template.json`'s `guids` array so each scaffolded project gets a fresh one. Aspire stores the container passwords it generates there, and they must stay in step with the data volumes.
- To check the wiring without starting containers: `dotnet run --project src/Template.AppHost -- --publisher manifest --output-path manifest.json`.

## Configuration conventions

- Bind options with **`IOptions<T>`** / **`Configure<T>(section)`** using section names that match the options class (e.g. `RedisOptions`, `MessagingOptions`, `RabbitMQOptions`, `ApplicationOptions`).
- **Redis**: `RedisOptions` (`ConnectionString`, `InstanceName`); wired in Infrastructure via `RedisCacheOptionsConfigurator` + `AddStackExchangeRedisCache`.
- **Messaging**: `RabbitMQOptions` is applied to Wolverine's **`ConnectionFactory`** (`UseRabbitMq(factory => ...)`), *not* turned into a URI — Wolverine's `Uri` overload only accepts an **`amqp://`** scheme and rejects MassTransit's old `rabbitmq://`. `MessagingOptions` (immediate retries, scheduled redelivery) maps onto one chained rule per broker handler chain: `chain.OnAnyException().RetryWithCooldown(...)` then `.Then.ScheduleRetry(...)` — applied by `BrokerResiliencePolicy`, not globally (see below).
- Wolverine config lives in `AddWolverine(...)`, which runs before a provider exists, so read it from **`IConfiguration`** directly rather than `IOptions<T>`.
- **Retries are scoped, never global.** `opts.Policies.OnAnyException()` applies to `InvokeAsync` too, so a global retry policy makes a failed CQRS command retry on every cooldown before the caller sees the error — a validation failure becomes a multi-second hang instead of an immediate 400. `BrokerResiliencePolicy` (an `IHandlerPolicy`) applies the rules only to the broker contracts' handler chains, which is where MassTransit's receive-endpoint retries lived.
- **`ServiceLocationPolicy.AlwaysAllowed` is required.** Wolverine's codegen inlines dependency construction and by default rejects container lookups. Infrastructure aliases `IApplicationContext` to the `ApplicationContext` instance via a lambda, which Wolverine cannot see through, so every handler taking `IApplicationContext` would fail with `InvalidServiceLocationException`. Registering the interface against the concrete type instead would hand out a second `DbContext` per scope — a quieter and worse bug.
- **Validators are registered once, by `UseFluentValidation()`.** It defaults to `DiscoverAndRegisterValidators`; adding `AddValidatorsFromAssembly` as well registers each validator twice and every rule then runs twice.
- **`WolverineFx.RuntimeCompilation`** is a required package, not an optional extra: core WolverineFx 6.x dropped the Roslyn runtime compiler and the host throws on startup without it.
- Do **not** reintroduce a flat `"Redis"` string key; use **`RedisOptions`** in JSON.

## SDK

- `global.json` pins **`10.0.100`** with **`rollForward: "latestFeature"`** so any installed .NET 10 SDK in the feature band can build. If CI requires an exact patch, align `global.json` or install that SDK.

## Template authoring

- When adding symbols or conditional files, update **`.template.config/template.json`** and any **`README.md`** installation or configuration docs that describe template flags.

## Style for edits

- Match existing naming, file layout, and comment density in touched files.
- Scope changes to the requested behavior; avoid unrelated refactors or new markdown unless the user asks.
- After substantive C# changes, run **`dotnet build`** from the repo root (network may be needed for restore).

For human-oriented documentation, prefer **`README.md`**.
