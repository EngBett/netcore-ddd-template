# DDD .NET Template

A production-ready .NET 10 project template built on **Domain-Driven Design (DDD)** and **Clean Architecture** principles. It ships with CQRS and **RabbitMQ** messaging both handled by **Wolverine** (example consumer), Entity Framework Core (SQL Server, PostgreSQL, SQLite, or MySQL), JWT authentication, Serilog structured logging, Redis caching, Prometheus metrics, a **.NET Aspire** app host for local orchestration, a **Reqnroll** BDD test suite, and your choice of three API styles: traditional **MVC Controllers**, **Minimal APIs**, or **FastEndpoints**.

> **Licensing note.** This template uses **Wolverine** for both in-process CQRS and broker messaging, in place of MediatR and MassTransit. Both of those moved to commercial licences; WolverineFx is MIT, so a service scaffolded from this template carries no per-seat licence obligation for its dispatcher or its message bus — and there is one library to learn instead of two.

## Table of Contents

- [What Is This Template?](#what-is-this-template)
- [Architecture Overview](#architecture-overview)
- [Key Technologies](#key-technologies)
- [Installation](#installation)
- [Usage](#usage)
  - [Template Options](#template-options)
  - [Database providers](#database-providers)
  - [API Styles](#api-styles)
- [Project Structure](#project-structure)
  - [Template.Api](#templateapi)
  - [Template.Application](#templateapplication)
  - [Template.Domain](#templatedomain)
  - [Template.Infrastructure](#templateinfrastructure)
  - 
  - [Template.Common](#templatecommon)
- [Data Flow (Request Lifecycle)](#data-flow-request-lifecycle)
- [Configuration](#configuration)
- [Messaging (Wolverine + RabbitMQ)](#messaging-wolverine--rabbitmq)
- [Running Locally](#running-locally)
- [Local Orchestration (Aspire)](#local-orchestration-aspire)
- [Testing](#testing)
- [Adding Features](#adding-features)
- [Publishing the Template to NuGet](#publishing-the-template-to-nuget)

---

## Analyzer-clean scaffolding

A generated service builds with **zero warnings** under strict analysis
(`TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`, `NuGetAudit`), so a
project that turns those on starts from a clean slate instead of a backlog.

Two caveats, neither of them template code, and neither visible under this
template's own default settings:

- **SQLite.** `SQLitePCLRaw.lib.e_sqlite3` (transitive, via the EF SQLite
  provider) is covered by [GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q),
  high severity, affecting `<= 2.1.11`. **There is no patched release**, so it
  cannot be pinned away. A project with `NuGetAudit` enabled will see it.
- **MySQL.** `Pomelo.EntityFrameworkCore.MySql` has **no EF Core 10 release**;
  9.0.0 requires EF Core 9 and conflicts with this template's EF 10 pins. A
  project with `TreatWarningsAsErrors` will see NU1608. Use `--postgres` or
  `--mssql` on .NET 10 until Pomelo ships EF 10 support.

## What Is This Template?

This template gives you a fully wired-up, opinionated starting point for building a **.NET 10 Web API** that follows:

- **Domain-Driven Design (DDD)** — your business logic lives in a rich `Domain` layer with domain events, not in controllers or services.
- **Clean Architecture** — dependencies always point inward: `Api` → `Application` → `Domain`; `Infrastructure` implements interfaces defined in `Application`.
- **CQRS** — commands and queries are plain classes dispatched through Wolverine's `IMessageBus.InvokeAsync<TResponse>(...)`, keeping reads and writes separate. There are no marker interfaces to implement: a class named `<Something>Handler` with a `Handle` method is found by convention.
- `**IApplicationContext`** — handlers use a single EF Core context abstraction (`Set<T>()`, `SaveChangesAsync`, …) so the Application layer does not depend on a generic repository or separate unit-of-work type; Infrastructure supplies one `DbContext` implementation.
- **Database choice** — when you create a project, pick **SQL Server**, **PostgreSQL**, **SQLite**, or **MySQL**; the template wires the matching EF Core provider, packages, and sample `appsettings.json` for that database.
- **Messaging** — **Wolverine** is configured in `src/Template.Application/DependencyInjection.cs` to use **RabbitMQ** (`RabbitMQOptions` in configuration). Example: `TodoMessageConsumer` consumes `TodoMessage` from the broker (**`AutoProvision()`** declares the topology on start; each contract's queue is named in the `BrokerContracts` list).
- **One dispatcher, two jobs** — Wolverine is both the mediator and the message bus. Which messages leave the process is decided by the `BrokerContracts` list in `src/Template.Application/DependencyInjection.cs`; everything else runs on an in-process local queue.
- **One command to run it all** — a **.NET Aspire** app host starts the database, Redis, RabbitMQ and Seq alongside the API. The app host injects the same configuration keys the service already reads, so the service runs identically without Aspire.
- **Executable specifications** — a **Reqnroll** suite in `tests/` runs the service's real composition root against in-memory infrastructure, so `dotnet test` needs no Docker.

Every concern is separated into its own project, making the codebase easy to navigate, test, and extend.

---

## Architecture Overview

```
┌─────────────────────────────────────────┐
│              Template.Api               │  ← HTTP layer: receives requests,
│   (Controllers / Minimal / FastEndpts)  │    returns responses
└───────────────┬─────────────────────────┘
                │ dispatches via Wolverine
┌───────────────▼─────────────────────────┐
│          Template.Application           │  ← CQRS handlers, validation,
│     Commands · Queries · Behaviors      │    pipeline behaviours
└───────────────┬─────────────────────────┘
                │ uses domain models & interfaces
┌───────────────▼─────────────────────────┐
│            Template.Domain              │  ← Entities, value objects,
│      Entities · Events · Interfaces     │    domain events, exceptions
└─────────────────────────────────────────┘
                ▲ implements
┌───────────────┴─────────────────────────┐
│         Template.Infrastructure         │  ← EF Core `DbContext`,
│   DbContext impl · SQL · Redis wiring   │    SQL helpers, Redis
└─────────────────────────────────────────┘
                ▲ uses
┌───────────────┴─────────────────────────┐
│           Template.Common               │  ← Shared models, extensions,
│      Models · Extensions · Enums        │    response wrappers
└─────────────────────────────────────────┘
```

**Dependency rule**: inner layers have no reference to outer layers. `Domain` and `Application` never reference `Infrastructure` or `Api`.

---

## Key Technologies


| Technology                   | Purpose                                                                    |
| ---------------------------- | -------------------------------------------------------------------------- |
| **.NET 10**                  | Runtime and SDK                                                            |
| **ASP.NET Core 10**          | Web host, middleware pipeline                                              |
| **Entity Framework Core 10** | ORM (SQL Server, PostgreSQL, SQLite, or MySQL) and code-first migrations   |
| **FluentValidation 12**      | Request validation run as Wolverine middleware (`UseFluentValidation`)     |
| **Serilog**                  | Structured logging to console and Seq                                      |
| **Swashbuckle / OpenAPI**    | Swagger UI for API exploration                                             |
| **JWT Bearer**               | Authentication via `Microsoft.AspNetCore.Authentication.JwtBearer`         |
| **Redis**                    | Distributed caching via `StackExchange.Redis`                              |
| **Prometheus**               | Metrics scraping endpoint at `/metrics`                                    |
| **FastEndpoints 8**          | *(optional)* Slim, high-performance endpoint model                         |
| **IdentityModel**            | JWT claim helpers                                                          |
| **Wolverine 6**              | In-process CQRS *and* **RabbitMQ** messaging; MIT                          |
| **.NET Aspire 13**           | Local orchestration of dependencies (`aspire/` app host + shared ServiceDefaults)        |
| **OpenTelemetry**            | Tracing, metrics and logs via ServiceDefaults; exported only when OTLP is configured     |
| **Reqnroll 3**               | Gherkin specifications for the test suite, on xUnit                                      |


---

## Installation

Install the template globally from NuGet or directly from this repository:

```bash
# From NuGet (once published)
dotnet new install EngBett.DDD.Template

# From local source (for development / contribution)
dotnet new install /path/to/this/repo
```

Verify the template is registered:

```bash
dotnet new list ddd-template
```

### After installation

The template is available as `ddd-template` (see `shortName` in `.template.config/template.json`). You can pass [database flags](#database-providers) and [API style](#template-options) together, for example:

```bash
dotnet new ddd-template --name MyApp --postgres --apiStyle minimal
```

---

## Usage

```bash
# Default: MVC controllers + SQL Server sample appsettings
dotnet new ddd-template --name MyApp

# PostgreSQL (shortcut flag or explicit database choice)
dotnet new ddd-template --name MyApp --postgres
dotnet new ddd-template --name MyApp --database postgres

# SQLite
dotnet new ddd-template --name MyApp --sqlite

# MySQL
dotnet new ddd-template --name MyApp --mysql

# SQL Server (explicit; same as default when no DB flags are passed)
dotnet new ddd-template --name MyApp --mssql

# Minimal APIs
dotnet new ddd-template --name MyApp --apiStyle minimal

# FastEndpoints
dotnet new ddd-template --name MyApp --apiStyle fastendpoints

# Combine database + API style
dotnet new ddd-template --name MyApp --database sqlite --apiStyle fastendpoints

# Short form for database choice
dotnet new ddd-template --name MyApp -db postgres

# Custom output directory
dotnet new ddd-template --name MyApp --output ./src/MyApp
```

`--name` controls the **project name** (replaces every occurrence of `Template` in namespaces, file names, and solution). A folder with that name is created automatically.

### Template Options


| Option               | Values                                      | Default       | Description                                                            |
| -------------------- | ------------------------------------------- | ------------- | ---------------------------------------------------------------------- |
| `--apiStyle` (`-ap`) | `controllers` · `minimal` · `fastendpoints` | `controllers` | Selects the HTTP endpoint pattern                                      |
| `--database` (`-db`) | `mssql` · `postgres` · `sqlite` · `mysql`   | `mssql`       | Selects the EF Core relational provider and sample connection settings |
| `--postgres`         | boolean                                     | `false`       | Shortcut for `--database postgres`                                     |
| `--mysql`            | boolean                                     | `false`       | Shortcut for `--database mysql`                                        |
| `--sqlite`           | boolean                                     | `false`       | Shortcut for `--database sqlite`                                       |
| `--mssql`            | boolean                                     | `false`       | Shortcut for `--database mssql` (explicit SQL Server)                  |


If several `--postgres` / `--mysql` / `--sqlite` flags are passed together, resolution order is: **postgres**, then **mysql**, then **sqlite**. Otherwise the `--database` choice applies (default **mssql** when no flags are set).

### Database providers

The generated **Api** project references every EF Core provider package; at runtime the active provider is selected from `**DatabaseKind`** in `appsettings.json` (`mssql`, `postgres`, `sqlite`, or `mysql`). The template ships alternate sample files (`appsettings.Database.*.json`) and renames the selected one to `appsettings.json` when you run `dotnet new`, so you get a matching `**DATABASE_CON**` for local development.


| Provider             | `DatabaseKind` | Notes                                                                                                                                                                    |
| -------------------- | -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Microsoft SQL Server | `mssql`        | `UseSqlServer`, `Microsoft.EntityFrameworkCore.SqlServer`                                                                                                                |
| PostgreSQL           | `postgres`     | `UseNpgsql`, Npgsql provider                                                                                                                                             |
| SQLite               | `sqlite`       | `UseSqlite`; ensure the `data` folder exists or adjust the path in `DATABASE_CON`                                                                                        |
| MySQL                | `mysql`        | `UseMySql` via Pomelo; server version in code is pinned to **MySQL 8.0.36**—adjust in `src/Template.Infrastructure/DependencyInjection.cs` if you use another server version |


**Updating an existing project:** set `DatabaseKind` and `DATABASE_CON` in configuration to switch providers; no need to re-run the template.

### API Styles

#### `controllers` — MVC Controllers (default)

The classic ASP.NET Core pattern. Each resource group is a controller class that inherits `BaseController`.

```
src/MyApp.Api/
└── Controllers/
    ├── BaseController.cs      # Shared logic: maps ApiResponse codes to HTTP status codes
    └── V1/
        └── TestController.cs  # Example: GET /api/v1/test
```

Add a new controller:

```csharp
[ApiController]
[Route("api/v1/[controller]")]
public class ProductsController : BaseController
{
    private readonly IMessageBus _bus;
    public ProductsController(IMessageBus bus) => _bus = bus;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GetProductsQuery query)
        => CustomResponse(await _bus.InvokeAsync<ApiResponse<List<ProductDto>>>(query));
}
```

#### `minimal` — Minimal APIs

Endpoints are plain lambda functions registered in `MinimalApiEndpoints/MinimalApiEndpointRegistration.cs`.

```
src/MyApp.Api/
└── MinimalApiEndpoints/
    └── MinimalApiEndpointRegistration.cs  # Groups & registers all minimal endpoints
```

Add a new endpoint group:

```csharp
// In MinimalApiEndpointRegistration.cs
public static WebApplication MapMinimalApiEndpoints(this WebApplication app)
{
    app.MapTestEndpoints();
    app.MapProductEndpoints();  // ← add here
    return app;
}

// New file: ProductEndpoints.cs
private static void MapProductEndpoints(this WebApplication app)
{
    var group = app.MapGroup("/api/v1/products").RequireAuthorization();
    group.MapGet("/", async (IMessageBus bus) =>
        Results.Ok(await bus.InvokeAsync<ApiResponse<List<ProductDto>>>(new GetProductsQuery())));
}
```

#### `fastendpoints` — FastEndpoints

Each endpoint is a self-contained class. FastEndpoints discovers them automatically at startup.

```
src/MyApp.Api/
└── Endpoints/
    └── TestEndpoint.cs   # Example: GET /api/v1/test
```

Add a new endpoint:

```csharp
public class GetProductsEndpoint : EndpointWithoutRequest<ApiResponse<List<ProductDto>>>
{
    private readonly IMessageBus _bus;
    public GetProductsEndpoint(IMessageBus bus) => _bus = bus;

    public override void Configure()
    {
        Get("/api/v1/products");
        RequireAuthorization();
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _bus.InvokeAsync<ApiResponse<List<ProductDto>>>(new GetProductsQuery()), ct);
}
```

See the [FastEndpoints documentation](https://fast-endpoints.com/) for the full API.

---

## Project Structure

```
MyApp/
├── global.json                            # Pins .NET 10 SDK
├── MyApp.sln                              # Solution file
│
├── src/
│   ├── MyApp.Api/                             # HTTP entry-point project
│   │   ├── Controllers/                       # [controllers style] MVC controller classes
│   │   │   ├── BaseController.cs              #   Shared HTTP-response helper
│   │   │   └── V1/
│   │   │       └── TestController.cs          #   Example controller
│   │   ├── MinimalApiEndpoints/               # [minimal style] Minimal API registrations
│   │   │   └── MinimalApiEndpointRegistration.cs
│   │   ├── Endpoints/                         # [fastendpoints style] FastEndpoints classes
│   │   │   └── TestEndpoint.cs
│   │   ├── Filters/
│   │   │   ├── ClientErrorResponse.cs         # Shared map: validation / domain / unique-constraint exception → 400 payload
│   │   │   ├── ServerErrorResponse.cs         # Shared 500 payload with a logged error code
│   │   │   ├── GlobalExceptionFilter.cs       # Translates exceptions → HTTP error responses (controllers only)
│   │   │   └── ExceptionResponseMiddleware.cs # Same responses for Minimal API / FastEndpoints
│   │   ├── Services/
│   │   │   └── CurrentUserService.cs          # Reads claims from the JWT token
│   │   ├── Properties/
│   │   │   └── launchSettings.json
│   │   ├── appsettings.json                   # Active config (`DatabaseKind`, `DATABASE_CON`, …); chosen at template creation
│   │   ├── appsettings.Database.postgres.json # Template-only: copied/renamed when using `--database postgres` / `--postgres`
│   │   ├── appsettings.Database.sqlite.json   # Template-only: SQLite sample
│   │   ├── appsettings.Database.mysql.json    # Template-only: MySQL sample
│   │   ├── Program.cs                         # Host bootstrap; calls Api / Application / Infrastructure DI extensions
│   │   ├── DependencyInjection.cs             # HTTP pipeline: controllers, Swagger, CORS, JWT wiring (calls Infrastructure for auth)
│   │   └── Dockerfile                         # Multi-stage Docker build
│   │
│   ├── MyApp.Application/                     # CQRS / use-case layer
│   │   ├── Features/                          # Feature slices (vertical folders)
│   │   │   └── Todos/                         # Example feature area
│   │   │       ├── Commands/                  # command + its handler, for writes
│   │   │       ├── Queries/                   # query + its handler, for reads
│   │   │       ├── Validators/                # FluentValidation rules for requests in this feature
│   │   │       ├── Models/                    # DTOs / read models for this feature (e.g. TodoDto)
│   │   │       └── EventHandlers/             # <Event>Handler classes for domain events (cross-feature OK)
│   │   ├── Interfaces/
│   │   │   ├── ICurrentUserService.cs         # Abstraction for reading the current user
│   │   │   └── IApplicationContext.cs         # Abstraction over EF Core (implemented by `ApplicationContext`)
│   │   ├── Consumers/                         # Wolverine message consumers, discovered by convention (e.g. RabbitMQ)
│   │   └── DependencyInjection.cs             # Wolverine: handler discovery, validation, RabbitMQ, broker contracts
│   │
│   ├── MyApp.Domain/                          # Core business layer (no infrastructure dependencies)
│   │   ├── Models/
│   │   │   ├── BaseEntity.cs                  # Base class: Id, DateCreated, DateUpdated, IsDeleted,
│   │   │   │                                  #   domain-event collection, equality by Id
│   │   │   └── DatabaseSequence.cs            # Enum: SQL Server sequences (use [Description] for DB name)
│   │   ├── Exceptions/
│   │   │   └── DomainException.cs             # Throw for business-rule violations (caught by GlobalExceptionFilter)
│   │   ├── Interfaces/
│   │   │   └── ISpecifications.cs             # Specification pattern contract
│   │   └── DomainEvents/                      # IDomainEvent marker + events (e.g. Todos/TodoCreatedEvent)
│   │
│   ├── MyApp.Infrastructure/                  # External-system implementations
│   │   ├── DependencyInjection.cs             # EF Core, Redis cache, JWT authentication
│   │   ├── DataAccess/
│   │   │   ├── ApplicationContext.cs          # EF Core DbContext; implements IApplicationContext;
│   │   │   │                                  #   overrides SaveChangesAsync to dispatch domain events
│   │   │   └── Extension/
│   │   │       └── ApiContextExtension.cs     # DbContext helpers (e.g. sequence helpers)
│   │   └── Extensions/
│   │       ├── DomainEventDispatcher.cs       # DispatchDomainEventsAsync — invoked from ApplicationContext.SaveChangesAsync
│   │       ├── QueryableExtension.cs          # IQueryable helpers
│   │       ├── SqlExtension.cs                # Raw SQL mapping helpers
│   │       └── SqlScriptsMigrationBuilder.cs  # Run embedded SQL scripts during migrations
│   │
│   └── MyApp.Common/                          # Cross-cutting concerns shared across all layers
│       ├── Options/
│       │   ├── ApplicationOptions.cs          # Strongly-typed binding for the `ApplicationOptions` section in appsettings
│       │   ├── RedisOptions.cs                 # `RedisOptions`: connection string + cache key prefix
│       │   ├── RabbitMQOptions.cs              # `RabbitMQOptions`: Wolverine RabbitMQ host/user/vhost
│       │   └── MessagingOptions.cs             # `MessagingOptions`: retries, scheduled redelivery
│       ├── Messages/                          # Contracts published/consumed via Wolverine (e.g. Todos/TodoMessage)
│       ├── Models/
│       │   ├── ApiResponseModel.cs            # ApiResponse<T> and ResponseMessage helpers
│       │   ├── LogModel.cs                    # Structured log entry shape
│       │   ├── PagedResult.cs                 # Generic pagination wrapper
│       │   └── ResponseEnums.cs               # ResponseCodes enum: Success, Fail, NotFound, …
│       └── Extensions/
│           ├── EnumUtilExtension.cs           # Enum description/display helpers
│           ├── GenericTypeExtensions.cs        # GetGenericTypeName() helper for logging type names
│           └── QueryableExtension.cs          # Pagination and ordering helpers
│
├── aspire/                                # .NET Aspire projects
│   ├── MyApp.AppHost/                     # Local orchestration: declares rabbitmq/redis/seq/
│   │   │                                  #   database and maps each to the config keys the
│   │   │                                  #   API already binds. Only project using Aspire
│   │   │                                  #   Hosting packages.
│   │   ├── Program.cs
│   │   └── MyApp.AppHost.csproj
│   └── MyApp.ServiceDefaults/             # Shared host wiring referenced by the service
│       ├── Extensions.cs                  #   AddServiceDefaults(): OpenTelemetry, health
│       │                                  #   checks, HttpClient resilience. No service
│       │                                  #   discovery — see the file's remarks.
│       └── MyApp.ServiceDefaults.csproj
│
└── tests/
    └── MyApp.Tests/                       # Reqnroll (Gherkin) specifications
        ├── Features/                      #   Cqrs / DomainEvents / BrokerRouting / HttpResponses .feature
        ├── Steps/                          #   Step definitions
        └── Support/                        #   Boots the real composition root once per run
```

---

## Data Flow (Request Lifecycle)

Here is how an HTTP request travels through the layers:

```
HTTP Request
    │
    ▼
[Template.Api] Controller / Minimal endpoint / FastEndpoints endpoint
    │  Injects IMessageBus, calls InvokeAsync<TResponse>(command or query)
    ▼
[Template.Application] Wolverine handler chain
    │  1. FluentValidation middleware — throws ValidationException on failure
    │  2. YourCommandHandler / YourQueryHandler — executes the use case
    │     ├── Uses IApplicationContext (Set<T>(), Add, queries, …) for persistence
    │     └── Calls IApplicationContext.SaveChangesAsync() to commit
    ▼
[Template.Infrastructure] ApplicationContext.SaveChangesAsync()
    │  1. EF Core persists changes to SQL Server
    │  2. DomainEventDispatcher.DispatchDomainEventsAsync() invokes any domain events
    ▼
[Template.Application] Domain event handlers (plain <Event>Handler classes)
    │
    ▼
[Template.Api] Handler returns result → ApiResponse<T> → HTTP response
```

**Error handling**: unhandled exceptions bubble up to `GlobalExceptionFilter`, which maps:

- `ValidationException` → `400 Bad Request`, with `message` set to the first failure and `errors` listing them all
- `DomainException` → `400 Bad Request`
- EF Core **unique-constraint** violations (SQL Server, PostgreSQL, SQLite, MySQL) → `400 Bad Request` with a human-readable or provider message
- Any other exception → `500 Internal Server Error` (with full detail in Development)

`GlobalExceptionFilter` is an MVC `IExceptionFilter`, so it only runs for the **controllers** style. The Minimal API and FastEndpoints styles need their own mapping or they would return a bare `500` for a bad request. `ExceptionResponseMiddleware` (registered first in `ConfigureMiddleware`) covers them. Both use `ClientErrorResponse` for the `400`s and `ServerErrorResponse` for the `500`, so every case above produces the identical status and payload in every style:

```json
{ "result": null, "message": "'User Id' must not be empty.", "errors": ["'User Id' must not be empty."] }
```

A server fault gets a `500` whose `message` ends in `Error Code: <id>`; the same id is on the logged exception. In Development the message carries the full exception instead of the developer exception page, the same for every style. The middleware leaves two cases alone: a response that has already started, and a request the client aborted.

A handler that returns an `ApiResponse<T>` with `Code = NotFound` or `Fail` gets a `404` or `400` in every style: `BaseController.CustomResponse` does it for controllers, and `ApiResponseResults.ToHttpResult()` for Minimal API and FastEndpoints. Keep the two in step. Note that Wolverine also logs each validation failure at `Error` level; tune that with `opts.Policies.MessageExecutionLogLevel(...)` if client errors are noisy in your logs.

---

## Configuration

All settings live in `appsettings.json`. Override them with environment variables or an `appsettings.{Environment}.json` file. Host, JWT, logging, and Serilog-related settings are grouped under `**ApplicationOptions`** (`src/Template.Common/Options/ApplicationOptions.cs`). **Distributed cache** uses `**RedisOptions`** (`src/Template.Common/Options/RedisOptions.cs`), wired in `**src/Template.Infrastructure/DependencyInjection.cs**`. **Wolverine** reads `**RabbitMQOptions`**, `**MessagingOptions**` (retries, scheduled redelivery), and registers consumers in `**src/Template.Application/DependencyInjection.cs**`. The HTTP pipeline lives in `**src/Template.Api/DependencyInjection.cs**`—see `Program.cs`.

```json
{
  "DatabaseKind": "mssql",
  "DATABASE_CON": "Server=localhost,1433;Database=MyApp;User Id=sa;Password=YourPassword;TrustServerCertificate=True;",
  "RedisOptions": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "MyApp.Api"
  },
  "MessagingOptions": {
    "RetryIntervalsMilliseconds": [ 100, 500, 1000 ],
    "EnableDelayedRedelivery": true,
    "RedeliveryIntervalsSeconds": [ 1, 5, 15 ]
  },
  "RabbitMQOptions": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/"
  },
  "ApplicationOptions": {
    "LogUrl": "http://localhost:5341",
    "AllowedOrigins": [],
    "Authority": "",
    "Audience": "/resources",
    "Queue": "",
    "ClientId": "",
    "ClientSecret": "",
    "SensitiveDataKeys": "pan,authorization,secret,...",
    "SensitiveDataDefaultValues": "pan,authorization,...",
    "EnableAutoMigration": true,
    "UseLoggerMiddleWare": true,
    "RequireHttpsMetadata": true,
    "MetadataAddress": "/.well-known/openid-configuration",
    "ShowSwagger": true
  },
  "Logging": {
    "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
  }
}
```


| Key                                             | Description                                                                                                           |
| ----------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| `DatabaseKind`                                  | Active EF Core provider: `mssql`, `postgres`, `sqlite`, or `mysql` (must match packages and connection string format) |
| `DATABASE_CON`                                  | Database connection string for the selected provider                                                                  |
| `RedisOptions.ConnectionString`                 | StackExchange.Redis connection (e.g. `host:port` or full connection string)                                           |
| `RedisOptions.InstanceName`                     | Prefix for cache keys when using `IDistributedCache`                                                                  |
| `MessagingOptions.RetryIntervalsMilliseconds`   | Immediate in-process retry delays (ms); omit or use `[]` to skip immediate retries                                    |
| `MessagingOptions.EnableDelayedRedelivery`      | When true, messages that exhaust the immediate retries are rescheduled for later attempts                              |
| `MessagingOptions.RedeliveryIntervalsSeconds`   | Scheduled retry schedule (seconds) when enabled                                                                       |
| `RabbitMQOptions.HostName`                      | RabbitMQ server hostname (Wolverine)                                                                                  |
| `RabbitMQOptions.Port`                          | AMQP port (default **5672**)                                                                                          |
| `RabbitMQOptions.UserName` / `Password`         | Broker credentials                                                                                                    |
| `RabbitMQOptions.VirtualHost`                   | Virtual host (e.g. `**/`** for the default vhost)                                                                     |
| `ApplicationOptions.LogUrl`                     | Seq or other structured log sink URL (used when configuring Serilog)                                                  |
| `ApplicationOptions.AllowedOrigins`             | Browser origins allowed to call the API cross-origin, with credentials. Empty (the default) allows none               |
| `ApplicationOptions.Authority`                  | JWT authority (your identity provider URL)                                                                            |
| `ApplicationOptions.Audience`                   | JWT audience                                                                                                          |
| `ApplicationOptions.MetadataAddress`            | Optional OIDC metadata path or URL fragment                                                                           |
| `ApplicationOptions.SensitiveDataKeys`          | Comma-separated keys to redact in logs                                                                                |
| `ApplicationOptions.EnableAutoMigration`        | When true, `Program` applies EF Core migrations on startup                                                            |
| `ApplicationOptions.UseLoggerMiddleWare`        | Feature flag for request logging middleware (if wired)                                                                |
| `ApplicationOptions.RequireHttpsMetadata`       | Require the JWT authority's metadata over HTTPS (default **true**; turn off only for a local plain-HTTP identity provider) |
| `ApplicationOptions.ShowSwagger`                | Enables Swagger UI outside Development; it is always on in Development (`src/Template.Api/DependencyInjection.ConfigureMiddleware`) |


## Messaging (Wolverine + RabbitMQ)

- **Configuration** is bound from `**RabbitMQOptions`**, `**MessagingOptions**`, and (for cache) `**RedisOptions**` in `Template.Common` (see `appsettings.json`).
- **Registration** lives in `**src/Template.Application/DependencyInjection.cs`**: `AddWolverine` applies `RabbitMQOptions` to the RabbitMQ `**ConnectionFactory**` and `**AutoProvision()**` declares the topology on start, so a fresh broker needs no manual setup.
- **Example consumer**: `src/Template.Application/Consumers/TodoMessageConsumer.cs`. Wolverine has **no `IConsumer<T>` to implement**—a class whose name ends in `Consumer` (or `Handler`) with a `Consume`/`Handle` method is discovered by convention, and the message type is taken from the first parameter. The message type is `src/Template.Common/Messages/Todos/TodoMessage.cs`.
- **Publishing**: inject `**IMessageBus**` and call `**PublishAsync**` / `**SendAsync**` with `TodoMessage` (or your own contract types). This replaces MassTransit's `IPublishEndpoint` / `ISendEndpointProvider` / `IBus`.

### Durable messaging (PostgreSQL and SQL Server)

On by default for those two providers. Handlers that depend on `IApplicationContext` run in a single transaction that also stores outgoing messages and domain events (the transactional outbox), and incoming broker messages are recorded in an inbox. Configure it with `DurableMessagingOptions` in `appsettings.json`. Outside Development, create the schema once with `dotnet run --project src/Template.Api -- resources setup` and leave `AutoBuildStorage` false. See `AGENTS.md`, "Durable messaging", for the rules this imposes on handlers and sagas.

### What crosses the broker is opt-in

Because Wolverine is also the mediator, every command, query and domain event in the Application layer is a "message" to it. Broker routing is therefore declared per contract, in one place:

```csharp
// src/Template.Application/DependencyInjection.cs
private static readonly (Type Contract, string Queue)[] BrokerContracts =
[
    (typeof(TodoMessage), "todo-message")
];
```

That single list drives both the RabbitMQ routing and the retry policy. **A message type not listed here stays on an in-process local queue**—which is exactly what you want for CQRS requests and domain events. To add a broker contract, add it to this list.

`**UseConventionalRouting()**` is deliberately *not* used. It looks like the equivalent of MassTransit's `ConfigureEndpoints`, but when a message type has a local handler—as any contract whose consumer lives in the solution does—Wolverine resolves it to the local queue in preference to the broker, and published messages quietly never leave the process.

### Retries apply to broker messages only

`MessagingOptions` becomes one chained rule—`**OnAnyException().RetryWithCooldown(…)**` for immediate retries, then `**.Then.ScheduleRetry(…)**` for a slower scheduled round—applied by `BrokerResiliencePolicy` to the handler chains of the broker contracts.

It is applied there rather than through `opts.Policies` because a global policy also governs `InvokeAsync`. With the template's default intervals, a command that fails validation would be retried at 100 ms, 500 ms and 1 s, then rescheduled, so the caller waits **seconds** for what should be an immediate `400`. MassTransit's retries lived on receive endpoints and never touched MediatR; scoping the rules to broker contracts keeps that separation.

### Two Wolverine settings this template must keep

- **`WolverineFx.RuntimeCompilation` is a required package, not an optional extra.** Core WolverineFx 6.x no longer ships the Roslyn runtime compiler, and the host throws on startup without it. The alternative is pre-generating handler code (`codegen write`) plus `TypeLoadMode.Static`, which suits a tuned deployment more than a template default.
- **`ServiceLocationPolicy.AlwaysAllowed`.** Wolverine's generated handler code constructs dependencies inline and by default refuses to fall back on the container. Infrastructure registers `IApplicationContext` as an alias so one `DbContext` serves both the interface and the concrete type, and Wolverine cannot see through that lambda—so every handler taking `IApplicationContext` would otherwise fail with `InvalidServiceLocationException`. Registering the interface against the concrete type instead would hand out a *second* `DbContext` per scope, with its own change tracker: a quieter and worse bug than one container lookup per invocation.

The API host starts **Wolverine** as a hosted service when the process starts; ensure RabbitMQ is reachable or startup will fail.

> **Migrating from the MediatR + MassTransit version of this template?** `IMediator` becomes `IMessageBus`, and `Send(x)` becomes `InvokeAsync<TResponse>(x)`—the response type is now stated at the call site. Drop `IRequest<T>` / `IRequestHandler<,>` / `INotificationHandler<>` from your messages and handlers; keep the `<Name>Handler.Handle` naming and they are found by convention. `ValidatorBehavior` is replaced by `UseFluentValidation()`. `MassTransitOptions` is now `MessagingOptions`, and its `EnableInMemoryOutbox` flag is gone: Wolverine always holds the messages a handler produces until that handler succeeds, so there was nothing left for the flag to switch off.

---

## Running Locally

**Prerequisites**: .NET 10 SDK, and a container runtime (Docker Desktop, Podman, or Rancher Desktop) if you use the Aspire AppHost.

1. **Install the template** (see [Installation](#installation)), then create a project with the database you need, for example:
  ```bash
   dotnet new install /path/to/this/repo
   dotnet new ddd-template --name MyApp --postgres --output ./MyApp
  ```

2. **Run everything with Aspire** — one command starts the database, Redis, RabbitMQ, Seq and the API, wired together:
  ```bash
   dotnet run --project aspire/MyApp.AppHost
  ```
   The Aspire dashboard opens with a link to each resource. See
   [Local Orchestration (Aspire)](#local-orchestration-aspire) for what it injects.

3. **Or run the API on its own**, against infrastructure you manage yourself:
  ```bash
   dotnet run --project src/MyApp.Api
  ```
   In this mode the API reads everything from its own `appsettings.json`, so bring up
   whatever matches it:
  ```bash
   # SQL Server (DatabaseKind: mssql)
   docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Password@123" \
              -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest

   # PostgreSQL (DatabaseKind: postgres)
   docker run -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=MyApp.Api \
              -p 5432:5432 -d postgres:16

   # MySQL (DatabaseKind: mysql)
   docker run -e MYSQL_ROOT_PASSWORD=root -e MYSQL_DATABASE=MyApp.Api \
              -p 3306:3306 -d mysql:8

   # SQLite needs no server — ensure `DATABASE_CON` path is writable (e.g. create `./data`).

   # Redis
   docker run -p 6379:6379 -d redis

   # RabbitMQ (Wolverine — matches default RabbitMQOptions)
   docker run -p 5672:5672 -p 15672:15672 -d rabbitmq:3-management

   # Seq (optional — structured log viewer)
   docker run -p 5341:5341 -p 80:80 -d datalust/seq
  ```
   Then update `appsettings.json` so `DATABASE_CON`, `**RedisOptions**`,
   `**RabbitMQOptions**`, and `**MessagingOptions**` match your environment.

4. **Browse:**
  - Swagger UI → `https://localhost:7254/swagger`
  - Health check → `https://localhost:7254/_health`
  - Metrics → `https://localhost:7254/metrics`

  Under Aspire the ports are assigned by the AppHost; use the dashboard's links instead.

---

## Local Orchestration (Aspire)

The `aspire/` folder holds two projects:

| Project                  | Role                                                                   |
| ------------------------ | ---------------------------------------------------------------------- |
| `MyApp.AppHost`          | Starts the service and its dependencies. The only project referencing Aspire **Hosting** packages. |
| `MyApp.ServiceDefaults`  | Shared host wiring the service itself references: OpenTelemetry, health checks, HttpClient resilience. |

`aspire/MyApp.AppHost` is a [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) app host. It starts the service together with the containers it depends on:

| Resource   | Purpose                                              |
| ---------- | ---------------------------------------------------- |
| `rabbitmq` | Wolverine's broker, with the management plugin enabled |
| `redis`    | `IDistributedCache` backing store                    |
| `seq`      | Structured log viewer that `ApplicationOptions.LogUrl` points at |
| database   | `sqlserver`, `postgres` or `mysql`, matching the provider you scaffolded with |

```bash
dotnet run --project aspire/MyApp.AppHost
```

### The AppHost is the only project referencing Aspire

The service itself references **no** Aspire package and uses **no** service discovery. The AppHost's whole job is to translate each resource into the *same* configuration keys the service already binds from `appsettings.json`:

| Config key                       | Comes from                          |
| -------------------------------- | ----------------------------------- |
| `DATABASE_CON`                   | the database resource's connection string |
| `RedisOptions:ConnectionString`  | the `redis` resource                |
| `RabbitMQOptions:HostName`/`:Port`/`:UserName`/`:Password` | the `rabbitmq` resource, whose credentials Aspire generates |
| `ApplicationOptions:LogUrl`      | the `seq` endpoint URL              |

Environment variables use `__` where configuration uses `:`, so `RabbitMQOptions__HostName` binds to `RabbitMQOptions:HostName`.

Two things follow from this, and both are the point:

- **Nothing is Aspire-only.** Every value the AppHost injects can be set in production as a plain environment variable or `appsettings` entry, with no Aspire in the picture.
- **The service still runs standalone.** `dotnet run --project src/MyApp.Api` works exactly as it did before, falling back to its own `appsettings.json`.

To see precisely what gets injected without starting any container:

```bash
dotnet run --project aspire/MyApp.AppHost -- --publisher manifest --output-path manifest.json
```

### ServiceDefaults, and the one thing it leaves out

`Program.cs` calls `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`. That gives every host:

- **OpenTelemetry** logging, metrics and tracing, including Wolverine's own `ActivitySource` and meter — so command and message handling shows up in traces rather than being the invisible majority of what the service does.
- **Health checks**, with a `self` check tagged `live`. `MapDefaultEndpoints()` maps `/alive` for liveness; `/_health` remains the readiness endpoint mapped by the API's own middleware.
- **HttpClient resilience** (`AddStandardResilienceHandler`): retries, circuit breaker and timeouts on every `HttpClient`.

**It deliberately does not call `AddServiceDiscovery()`**, which Aspire's stock ServiceDefaults does. Service discovery resolves logical names like `https://api` out of configuration the app host injects, which makes the service's HTTP targets depend on how it was launched — the one thing this template's configuration approach rules out, because it cannot be mirrored in production without reproducing Aspire's scheme. Everything that *is* included is portable: OTLP switches on via the standard `OTEL_EXPORTER_OTLP_ENDPOINT`, and resilience needs no configuration at all. If you add a second service later, give the caller an explicit base-address option rather than reintroducing discovery.

### Notes and limits

- **Add resources, not clients.** To add a dependency, add it in the AppHost and map it to a config key with `WithEnvironment`. Do not add Aspire client packages or `AddServiceDiscovery` to the service — that is what keeps production configuration transparent.
- **`DatabaseKind` is not injected.** The `appsettings.json` that ships with the provider you chose already sets it, and it stays the single source of truth.
- **SQLite has no resource.** It is a file, not a service, so the AppHost injects no connection string and the API keeps the `DATABASE_CON` from its own `appsettings.json`.
- **Serilog and Prometheus stay as they are.** ServiceDefaults adds OpenTelemetry alongside them rather than replacing them: Serilog still writes to console and Seq, `/metrics` still serves Prometheus, and OTLP export is switched on only when `OTEL_EXPORTER_OTLP_ENDPOINT` is present.
- **Serilog's sinks are added in code.** `Program.cs` adds Console, plus Seq at `ApplicationOptions.LogUrl` (which the AppHost sets). Don't also list them under `Serilog:WriteTo` in `appsettings.json`, or every log line is written twice.
- **In this repository the AppHost contains every database branch**, the same way `MyApp.Infrastructure` references every EF Core provider. `dotnet new` keeps exactly one. Running the template's own AppHost therefore starts more than one database server, which is why each branch names its database resource after its provider — Aspire rejects duplicate resource names.
---

## Testing

`tests/MyApp.Tests` is a [Reqnroll](https://reqnroll.net/) suite — Gherkin `.feature` files with C# step definitions, running on xUnit.

```bash
dotnet test
```

```
tests/MyApp.Tests/
├── Features/
│   ├── Cqrs.feature           # dispatch, validation, and that rejection is immediate
│   ├── DomainEvents.feature   # events reach handlers; an unhandled one does not fail the write
│   ├── BrokerRouting.feature  # only declared contracts are routed to RabbitMQ
│   └── HttpResponses.feature  # status codes and bodies a client sees, in any API style
├── Steps/                     # step definitions
└── Support/                   # TestHost, ApiHost, test DbContext, spy handler, query faults
```

### The suite runs the real composition root

`Support/TestHost.cs` calls the service's own `AddApplicationDependencies` rather than a hand-rolled stand-in, so the handler discovery, validation middleware, broker routing and retry scoping under test are the ones that ship. Exactly two things are substituted:

- **external transports are disabled**, so no RabbitMQ broker is needed;
- **the database is SQLite in memory**, so no server is needed.

That means `dotnet test` needs no Docker and no infrastructure, and it stays fast enough to run on every build.

### HTTP scenarios run the real API

`Support/ApiHost.cs` boots the API's own `Program` in memory with `WebApplicationFactory`, so `HttpResponses.feature` exercises whichever API style the service was scaffolded with — the same scenarios pass for controllers, Minimal APIs and FastEndpoints. It makes the same two substitutions as `TestHost`, plus two of its own: auto-migration is switched off, and `Support/QueryFaults.cs` adds a Wolverine middleware to the sample `GetTodosQuery` so a scenario can make it fail validation, break a business rule, report not found or throw. The endpoint, the exception filter or middleware, and the response mapping are all the shipped code.

Two details worth knowing if you extend it:

- `Support/SpyHandlers.cs` adds a *second* handler for the service's own `TodoCreatedEvent`. Wolverine invokes every handler it finds for a message, so this observes that dispatch really reached the concrete type without having to make the shipped handler observable. It is discovered because `TestHost` adds the test assembly to Wolverine's discovery.
- `Support/TestApplicationContext.cs` subclasses the real `ApplicationContext`, so `SaveChangesAsync` — and therefore domain-event dispatch — is the production code path. It only adds a `Widget` entity to hang events on.

### What it deliberately does not cover

The suite asserts behaviour through public APIs, so it stops where that would require reflecting into Wolverine's internals or standing up infrastructure:

- **Retry scoping is asserted by its consequence, not its configuration.** Rather than inspecting handler chains for failure rules, `Cqrs.feature` asserts that a rejected command comes back in under 250 ms. Apply the retry policy globally instead of per broker contract and that scenario fails — it took 8 seconds when tried.
- **Nothing talks to a real broker or database.** For that, `Aspire.Hosting.Testing` can start the AppHost's containers in a test; it needs a container runtime, so it is not wired up here.

---

## Adding Features

### 1 — Define a domain entity

```csharp
// src/MyApp.Domain/Models/Product.cs
public class Product : BaseEntity
{
    public string Name { get; private set; }
    public decimal Price { get; private set; }

    public static Product Create(string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Product name is required.");
        return new Product { Name = name, Price = price };
    }
}
```

### 2 — Add a command + handler

```csharp
// src/MyApp.Application/Features/Products/Commands/CreateProductCommand.cs

// No marker interface: Wolverine finds the handler by the `<Name>Handler.Handle` convention
// and takes the message type from the first parameter.
public record CreateProductCommand(string Name, decimal Price);

public class CreateProductHandler
{
    private readonly IApplicationContext _db;
    public CreateProductHandler(IApplicationContext db) => _db = db;

    public async Task<ApiResponse<string>> Handle(CreateProductCommand cmd, CancellationToken ct)
    {
        var product = Product.Create(cmd.Name, cmd.Price);
        await _db.Set<Product>().AddAsync(product, ct);
        await _db.SaveChangesAsync(ct);
        return ResponseMessage.Success(product.Id);
    }
}
```

### 3 — Add a validator

```csharp
// src/MyApp.Application/Features/Products/Validators/CreateProductValidator.cs
public class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
    }
}
```

### 4 — Register the entity with EF Core

```csharp
// src/MyApp.Infrastructure/DataAccess/ApplicationContext.cs
public DbSet<Product> Products => Set<Product>();
```

### 5 — Expose via endpoint

```csharp
// Controllers style
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateProductCommand cmd)
    => CustomResponse(await _bus.InvokeAsync<ApiResponse<string>>(cmd));
```

---

## Publishing the Template to NuGet

A `EngBett.DDD.Template.nuspec` is provided. Pack and push:

```bash
nuget pack EngBett.DDD.Template.nuspec -OutputDirectory ./nupkg
dotnet nuget push ./nupkg/*.nupkg \
    --source https://api.nuget.org/v3/index.json \
    --api-key YOUR_API_KEY
```

Once published, anyone can install it with:

```bash
dotnet new install EngBett.DDD.Template
```

