# DDD .NET Template

A production-ready .NET 10 project template built on **Domain-Driven Design (DDD)** and **Clean Architecture** principles. It ships with CQRS via MediatR, Entity Framework Core (SQL Server, PostgreSQL, SQLite, or MySQL), **MassTransit** with **RabbitMQ** (example consumer), JWT authentication, Serilog structured logging, Redis caching, Prometheus metrics, and your choice of three API styles: traditional **MVC Controllers**, **Minimal APIs**, or **FastEndpoints**.

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
  - [Template.Common](#templatecommon)
- [Data Flow (Request Lifecycle)](#data-flow-request-lifecycle)
- [Configuration](#configuration)
- [Messaging (MassTransit + RabbitMQ)](#messaging-masstransit--rabbitmq)
- [Running Locally](#running-locally)
- [Adding Features](#adding-features)
- [Publishing the Template to NuGet](#publishing-the-template-to-nuget)

---

## What Is This Template?

This template gives you a fully wired-up, opinionated starting point for building a **.NET 10 Web API** that follows:

- **Domain-Driven Design (DDD)** — your business logic lives in a rich `Domain` layer with domain events, not in controllers or services.
- **Clean Architecture** — dependencies always point inward: `Api` → `Application` → `Domain`; `Infrastructure` implements interfaces defined in `Application`.
- **CQRS** — commands and queries are first-class types dispatched through MediatR, keeping reads and writes separate.
- **`IApplicationContext`** — handlers use a single EF Core context abstraction (`Set<T>()`, `SaveChangesAsync`, …) so the Application layer does not depend on a generic repository or separate unit-of-work type; Infrastructure supplies one `DbContext` implementation.
- **Database choice** — when you create a project, pick **SQL Server**, **PostgreSQL**, **SQLite**, or **MySQL**; the template wires the matching EF Core provider, packages, and sample `appsettings.json` for that database.
- **Messaging** — **MassTransit** is configured in `Template.Application/DependencyInjection.cs` to use **RabbitMQ** (`RabbitMQOptions` in configuration). Example: `TodoMessageConsumer` consumes `TodoMessage` from the broker (queues/exchanges are created by MassTransit’s **topologies** when the bus starts).

Every concern is separated into its own project, making the codebase easy to navigate, test, and extend.

---

## Architecture Overview

```
┌─────────────────────────────────────────┐
│              Template.Api               │  ← HTTP layer: receives requests,
│   (Controllers / Minimal / FastEndpts)  │    returns responses
└───────────────┬─────────────────────────┘
                │ dispatches via MediatR
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

| Technology | Purpose |
|---|---|
| **.NET 10** | Runtime and SDK |
| **ASP.NET Core 10** | Web host, middleware pipeline |
| **Entity Framework Core 10** | ORM (SQL Server, PostgreSQL, SQLite, or MySQL) and code-first migrations |
| **MediatR 12** | In-process messaging for CQRS (commands, queries, domain events) |
| **FluentValidation 12** | Request validation wired into the MediatR pipeline |
| **Serilog** | Structured logging to console and Seq |
| **Swashbuckle / OpenAPI** | Swagger UI for API exploration |
| **JWT Bearer** | Authentication via `Microsoft.AspNetCore.Authentication.JwtBearer` |
| **Redis** | Distributed caching via `StackExchange.Redis` |
| **Prometheus** | Metrics scraping endpoint at `/metrics` |
| **FastEndpoints 8** | *(optional)* Slim, high-performance endpoint model |
| **IdentityModel** | JWT claim helpers |
| **MassTransit 8** | Asynchronous messaging; **RabbitMQ** transport with configurable consumers |

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

| Option | Values | Default | Description |
|--------|--------|---------|-------------|
| `--apiStyle` (`-ap`) | `controllers` · `minimal` · `fastendpoints` | `controllers` | Selects the HTTP endpoint pattern |
| `--database` (`-db`) | `mssql` · `postgres` · `sqlite` · `mysql` | `mssql` | Selects the EF Core relational provider and sample connection settings |
| `--postgres` | boolean | `false` | Shortcut for `--database postgres` |
| `--mysql` | boolean | `false` | Shortcut for `--database mysql` |
| `--sqlite` | boolean | `false` | Shortcut for `--database sqlite` |
| `--mssql` | boolean | `false` | Shortcut for `--database mssql` (explicit SQL Server) |

If several `--postgres` / `--mysql` / `--sqlite` flags are passed together, resolution order is: **postgres**, then **mysql**, then **sqlite**. Otherwise the `--database` choice applies (default **mssql** when no flags are set).

### Database providers

The generated **Api** project references every EF Core provider package; at runtime the active provider is selected from **`DatabaseKind`** in `appsettings.json` (`mssql`, `postgres`, `sqlite`, or `mysql`). The template ships alternate sample files (`appsettings.Database.*.json`) and renames the selected one to `appsettings.json` when you run `dotnet new`, so you get a matching **`DATABASE_CON`** for local development.

| Provider | `DatabaseKind` | Notes |
|----------|----------------|--------|
| Microsoft SQL Server | `mssql` | `UseSqlServer`, `Microsoft.EntityFrameworkCore.SqlServer` |
| PostgreSQL | `postgres` | `UseNpgsql`, Npgsql provider |
| SQLite | `sqlite` | `UseSqlite`; ensure the `data` folder exists or adjust the path in `DATABASE_CON` |
| MySQL | `mysql` | `UseMySql` via Pomelo; server version in code is pinned to **MySQL 8.0.36**—adjust in `Template.Infrastructure/DependencyInjection.cs` if you use another server version |

**Updating an existing project:** set `DatabaseKind` and `DATABASE_CON` in configuration to switch providers; no need to re-run the template.

### API Styles

#### `controllers` — MVC Controllers (default)

The classic ASP.NET Core pattern. Each resource group is a controller class that inherits `BaseController`.

```
MyApp.Api/
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
    private readonly IMediator _mediator;
    public ProductsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GetProductsQuery query)
        => CustomResponse(await _mediator.Send(query));
}
```

#### `minimal` — Minimal APIs

Endpoints are plain lambda functions registered in `MinimalApiEndpoints/MinimalApiEndpointRegistration.cs`.

```
MyApp.Api/
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
    group.MapGet("/", async (IMediator mediator) =>
        Results.Ok(await mediator.Send(new GetProductsQuery())));
}
```

#### `fastendpoints` — FastEndpoints

Each endpoint is a self-contained class. FastEndpoints discovers them automatically at startup.

```
MyApp.Api/
└── Endpoints/
    └── TestEndpoint.cs   # Example: GET /api/v1/test
```

Add a new endpoint:
```csharp
public class GetProductsEndpoint : EndpointWithoutRequest<ApiResponse<List<ProductDto>>>
{
    private readonly IMediator _mediator;
    public GetProductsEndpoint(IMediator mediator) => _mediator = mediator;

    public override void Configure()
    {
        Get("/api/v1/products");
        RequireAuthorization();
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _mediator.Send(new GetProductsQuery()), ct);
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
├── MyApp.Api/                             # HTTP entry-point project
│   ├── Controllers/                       # [controllers style] MVC controller classes
│   │   ├── BaseController.cs              #   Shared HTTP-response helper
│   │   └── V1/
│   │       └── TestController.cs          #   Example controller
│   ├── MinimalApiEndpoints/               # [minimal style] Minimal API registrations
│   │   └── MinimalApiEndpointRegistration.cs
│   ├── Endpoints/                         # [fastendpoints style] FastEndpoints classes
│   │   └── TestEndpoint.cs
│   ├── Filters/
│   │   └── GlobalExceptionFilter.cs       # Translates exceptions → HTTP error responses
│   ├── Services/
│   │   └── CurrentUserService.cs          # Reads claims from the JWT token
│   ├── Properties/
│   │   └── launchSettings.json
│   ├── appsettings.json                   # Active config (`DatabaseKind`, `DATABASE_CON`, …); chosen at template creation
│   ├── appsettings.Database.postgres.json # Template-only: copied/renamed when using `--database postgres` / `--postgres`
│   ├── appsettings.Database.sqlite.json   # Template-only: SQLite sample
│   ├── appsettings.Database.mysql.json    # Template-only: MySQL sample
│   ├── Program.cs                         # Host bootstrap; calls Api / Application / Infrastructure DI extensions
│   ├── DependencyInjection.cs             # HTTP pipeline: controllers, Swagger, CORS, JWT wiring (calls Infrastructure for auth)
│   └── Dockerfile                         # Multi-stage Docker build
│
├── MyApp.Application/                     # CQRS / use-case layer
│   ├── Behaviors/
│   │   └── ValidatorBehavior.cs           # MediatR pipeline: runs FluentValidation before handler
│   ├── Features/                          # Feature slices (vertical folders)
│   │   └── Todos/                         # Example feature area
│   │       ├── Commands/                  # IRequest handlers for writes
│   │       ├── Queries/                   # IRequest handlers for reads
│   │       ├── Validators/                # FluentValidation rules for requests in this feature
│   │       ├── Models/                    # DTOs / read models for this feature (e.g. TodoDto)
│   │       └── EventHandlers/             # INotificationHandler<T> for domain events (cross-feature OK)
│   ├── Interfaces/
│   │   ├── ICurrentUserService.cs         # Abstraction for reading the current user
│   │   └── IApplicationContext.cs         # Abstraction over EF Core (implemented by `ApplicationContext`)
│   ├── Consumers/                         # MassTransit `IConsumer<T>` handlers (e.g. RabbitMQ)
│   └── DependencyInjection.cs             # MediatR, FluentValidation, MassTransit + RabbitMQ (consumers)
│
├── MyApp.Domain/                          # Core business layer (no infrastructure dependencies)
│   ├── Models/
│   │   ├── BaseEntity.cs                  # Base class: Id, DateCreated, DateUpdated, IsDeleted,
│   │   │                                  #   domain-event collection, equality by Id
│   │   └── DatabaseSequence.cs            # Enum: SQL Server sequences (use [Description] for DB name)
│   ├── Exceptions/
│   │   └── DomainException.cs             # Throw for business-rule violations (caught by GlobalExceptionFilter)
│   ├── Interfaces/
│   │   └── ISpecifications.cs             # Specification pattern contract
│   └── DomainEvents/                      # INotification domain events (e.g. Todos/TodoCreatedEvent)
│
├── MyApp.Infrastructure/                  # External-system implementations
│   ├── DependencyInjection.cs             # EF Core, Redis cache, JWT authentication
│   ├── DataAccess/
│   │   ├── ApplicationContext.cs          # EF Core DbContext; implements IApplicationContext;
│   │   │                                  #   overrides SaveChangesAsync to dispatch domain events
│   │   └── Extension/
│   │       └── ApiContextExtension.cs     # DbContext helpers (e.g. sequence helpers)
│   └── Extensions/
│       ├── MediatorExtension.cs           # DispatchDomainEventsAsync — invoked from ApplicationContext.SaveChangesAsync
│       ├── QueryableExtension.cs          # IQueryable helpers
│       ├── SqlExtension.cs                # Raw SQL mapping helpers
│       └── SqlScriptsMigrationBuilder.cs  # Run embedded SQL scripts during migrations
│
└── MyApp.Common/                          # Cross-cutting concerns shared across all layers
    ├── Options/
    │   ├── ApplicationOptions.cs          # Strongly-typed binding for the `ApplicationOptions` section in appsettings
    │   ├── RedisOptions.cs                 # `RedisOptions`: connection string + cache key prefix
    │   ├── RabbitMQOptions.cs              # `RabbitMQOptions`: MassTransit RabbitMQ host/user/vhost
    │   └── MassTransitOptions.cs           # `MassTransitOptions`: retries, redelivery, in-memory outbox
    ├── Messages/                          # Contracts published/consumed via MassTransit (e.g. Todos/TodoMessage)
    ├── Models/
    │   ├── ApiResponseModel.cs            # ApiResponse<T> and ResponseMessage helpers
    │   ├── LogModel.cs                    # Structured log entry shape
    │   ├── PagedResult.cs                 # Generic pagination wrapper
    │   └── ResponseEnums.cs               # ResponseCodes enum: Success, Fail, NotFound, …
    └── Extensions/
        ├── EnumUtilExtension.cs           # Enum description/display helpers
        ├── GenericTypeExtensions.cs        # GetGenericTypeName() used by ValidatorBehavior
        └── QueryableExtension.cs          # Pagination and ordering helpers
```

---

## Data Flow (Request Lifecycle)

Here is how an HTTP request travels through the layers:

```
HTTP Request
    │
    ▼
[Template.Api] Controller / Minimal endpoint / FastEndpoints endpoint
    │  Injects IMediator, sends a Command or Query
    ▼
[Template.Application] MediatR Pipeline
    │  1. ValidatorBehavior — runs FluentValidation; throws ValidationException on failure
    │  2. YourCommandHandler / YourQueryHandler — executes the use case
    │     ├── Uses IApplicationContext (Set<T>(), Add, queries, …) for persistence
    │     └── Calls IApplicationContext.SaveChangesAsync() to commit
    ▼
[Template.Infrastructure] ApplicationContext.SaveChangesAsync()
    │  1. EF Core persists changes to SQL Server
    │  2. MediatorExtension.DispatchDomainEventsAsync() publishes any domain events
    ▼
[Template.Application] Domain event handlers (INotificationHandler<TEvent>)
    │
    ▼
[Template.Api] Handler returns result → ApiResponse<T> → HTTP response
```

**Error handling**: unhandled exceptions bubble up to `GlobalExceptionFilter`, which maps:
- `DomainException` → `400 Bad Request`
- EF Core **unique-constraint** violations (SQL Server, PostgreSQL, SQLite, MySQL) → `400 Bad Request` with a human-readable or provider message
- Any other exception → `500 Internal Server Error` (with full detail in Development)

---

## Configuration

All settings live in `appsettings.json`. Override them with environment variables or an `appsettings.{Environment}.json` file. Host, JWT, logging, and Serilog-related settings are grouped under **`ApplicationOptions`** (`Template.Common/Options/ApplicationOptions.cs`). **Distributed cache** uses **`RedisOptions`** (`Template.Common/Options/RedisOptions.cs`), wired in **`Template.Infrastructure/DependencyInjection.cs`**. **MassTransit** reads **`RabbitMQOptions`**, **`MassTransitOptions`** (retries, delayed redelivery, in-memory outbox), and registers consumers in **`Template.Application/DependencyInjection.cs`**. The HTTP pipeline lives in **`Template.Api/DependencyInjection.cs`**—see `Program.cs`.

```json
{
  "DatabaseKind": "mssql",
  "DATABASE_CON": "Server=localhost,1433;Database=MyApp;User Id=sa;Password=YourPassword;TrustServerCertificate=True;",
  "RedisOptions": {
    "ConnectionString": "localhost:6379",
    "InstanceName": "MyApp.Api"
  },
  "MassTransitOptions": {
    "EnableInMemoryOutbox": true,
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
    "Authority": "",
    "Audience": "/resources",
    "Queue": "",
    "ClientId": "",
    "ClientSecret": "secret",
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

| Key | Description |
|-----|-------------|
| `DatabaseKind` | Active EF Core provider: `mssql`, `postgres`, `sqlite`, or `mysql` (must match packages and connection string format) |
| `DATABASE_CON` | Database connection string for the selected provider |
| `RedisOptions.ConnectionString` | StackExchange.Redis connection (e.g. `host:port` or full connection string) |
| `RedisOptions.InstanceName` | Prefix for cache keys when using `IDistributedCache` |
| `MassTransitOptions.EnableInMemoryOutbox` | When true, MassTransit uses the in-memory outbox for send/publish with consumer retries |
| `MassTransitOptions.RetryIntervalsMilliseconds` | Immediate consumer retry delays (ms); omit or use `[]` to skip `UseMessageRetry` |
| `MassTransitOptions.EnableDelayedRedelivery` | When true, schedules extra delayed redelivery after retries (RabbitMQ transport) |
| `MassTransitOptions.RedeliveryIntervalsSeconds` | Delayed redelivery schedule (seconds) when enabled |
| `RabbitMQOptions.HostName` | RabbitMQ server hostname (MassTransit) |
| `RabbitMQOptions.Port` | AMQP port (default **5672**) |
| `RabbitMQOptions.UserName` / `Password` | Broker credentials |
| `RabbitMQOptions.VirtualHost` | Virtual host (e.g. **`/`** for the default vhost) |
| `ApplicationOptions.LogUrl` | Seq or other structured log sink URL (used when configuring Serilog) |
| `ApplicationOptions.Authority` | JWT authority (your identity provider URL) |
| `ApplicationOptions.Audience` | JWT audience |
| `ApplicationOptions.MetadataAddress` | Optional OIDC metadata path or URL fragment |
| `ApplicationOptions.SensitiveDataKeys` | Comma-separated keys to redact in logs |
| `ApplicationOptions.EnableAutoMigration` | When true, `Program` applies EF Core migrations on startup |
| `ApplicationOptions.UseLoggerMiddleWare` | Feature flag for request logging middleware (if wired) |
| `ApplicationOptions.RequireHttpsMetadata` | Passed to JWT bearer metadata retrieval when configured |
| `ApplicationOptions.ShowSwagger` | When true, Swagger UI is registered in the HTTP pipeline (`Template.Api/DependencyInjection.ConfigureMiddleware`) |

## Messaging (MassTransit + RabbitMQ)

- **Configuration** is bound from **`RabbitMQOptions`**, **`MassTransitOptions`**, and (for cache) **`RedisOptions`** in `Template.Common` (see `appsettings.json`).
- **Registration** lives in **`Template.Application/DependencyInjection.cs`**: `AddMassTransit` uses the **RabbitMQ** transport, optional **`AddConfigureEndpointsCallback`** + **`UseInMemoryOutbox(registrationContext)`** when `MassTransitOptions.EnableInMemoryOutbox` is true, `AddConsumer<T>()` registers consumers, **`UseMessageRetry`** / **`UseDelayedRedelivery`** apply resilience from `MassTransitOptions`, and **`ConfigureEndpoints`** creates receive endpoints (queue names follow MassTransit’s default **kebab-case** endpoint naming, e.g. for `TodoMessageConsumer`).
- **Example consumer**: `Template.Application/Consumers/TodoMessageConsumer.cs` implements `IConsumer<TodoMessage>`; the message type is `Template.Common/Messages/Todos/TodoMessage.cs`.
- **Publishing** from the API or application layer: inject **`IPublishEndpoint`** or **`ISendEndpointProvider`** (or the MassTransit **`IBus`**) and publish/send `TodoMessage` (or your own contract types) so the consumer can process them—add any new message types and consumers in the same way.
- The API host starts the **MassTransit bus** as a hosted service when the process starts; ensure RabbitMQ is reachable or startup will fail.

---

## Running Locally

**Prerequisites**: .NET 10 SDK, Docker (optional; Redis is optional if you change caching later).

1. **Install the template** (see [Installation](#installation)), then create a project with the database you need, for example:
   ```bash
   dotnet new install /path/to/this/repo
   dotnet new ddd-template --name MyApp --postgres --output ./src/MyApp
   ```

2. **Start infrastructure** that matches `DatabaseKind` in `appsettings.json`:

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

   # Redis (optional template default)
   docker run -p 6379:6379 -d redis

   # RabbitMQ (MassTransit — matches default RabbitMQOptions)
   docker run -p 5672:5672 -p 15672:15672 -d rabbitmq:3-management

   # Seq (optional — structured log viewer)
   docker run -p 5341:5341 -p 80:80 -d datalust/seq
   ```

3. **Update `appsettings.json`** so `DATABASE_CON`, **`RedisOptions`**, **`RabbitMQOptions`**, and **`MassTransitOptions`** match your environment.

4. **Run the API:**
   ```bash
   dotnet run --project MyApp.Api
   ```

5. **Browse:**
   - Swagger UI → `https://localhost:7254/swagger`
   - Health check → `https://localhost:7254/_health`
   - Metrics → `https://localhost:7254/metrics`

---

## Adding Features

### 1 — Define a domain entity

```csharp
// MyApp.Domain/Models/Product.cs
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
// MyApp.Application/Features/Products/Commands/CreateProductCommand.cs
public record CreateProductCommand(string Name, decimal Price) : IRequest<ApiResponse<string>>;

public class CreateProductHandler : IRequestHandler<CreateProductCommand, ApiResponse<string>>
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
// MyApp.Application/Features/Products/Validators/CreateProductValidator.cs
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
// MyApp.Infrastructure/DataAccess/ApplicationContext.cs
public DbSet<Product> Products => Set<Product>();
```

### 5 — Expose via endpoint

```csharp
// Controllers style
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateProductCommand cmd)
    => CustomResponse(await _mediator.Send(cmd));
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

