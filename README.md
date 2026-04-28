# DDD .NET Template

A .NET 10 Domain-Driven Design (DDD) template with Clean Architecture, MediatR, Entity Framework Core with SQL Server, Serilog, Redis caching, JWT authentication, and Prometheus metrics.

## Features

- **.NET 10** — targets the latest .NET runtime
- **Domain-Driven Design** — layered Clean Architecture: `Api`, `Application`, `Domain`, `Infrastructure`, `Common`
- **MediatR** — CQRS pattern with pipeline behaviours (validation, logging)
- **FluentValidation** — request validation via MediatR pipeline
- **Entity Framework Core 10** — SQL Server with migrations
- **Serilog** — structured logging to console and Seq
- **Redis** — distributed caching via `StackExchange.Redis`
- **JWT Bearer Authentication** — pre-configured JWT middleware
- **Swagger / OpenAPI** — Swashbuckle integration
- **Prometheus** — metrics endpoint at `/metrics`
- **Health Checks** — endpoint at `/_health`
- **Three API styles** — MVC controllers, Minimal APIs, or FastEndpoints

## Installation

Install the template via NuGet or directly from this repository:

```bash
# From NuGet (once published)
dotnet new install EngBett.DDD.Template

# From local source (development)
dotnet new install /path/to/this/repo
```

## Usage

```bash
# Create a project with traditional MVC controllers (default)
dotnet new ddd-template --name MyApp

# Create a project with ASP.NET Core Minimal APIs
dotnet new ddd-template --name MyApp --apiStyle minimal

# Create a project with FastEndpoints
dotnet new ddd-template --name MyApp --apiStyle fastendpoints
```

The `--name` option sets the project name. A directory with that name is created automatically when `--output` is not specified.

### Template Options

| Option | Values | Default | Description |
|--------|--------|---------|-------------|
| `--apiStyle` | `controllers`, `minimal`, `fastendpoints` | `controllers` | Selects the API pattern |

## API Styles

### Controllers (`--apiStyle controllers`)

Traditional MVC controllers. Provides a `Controllers/V1/TestController.cs` example with full DDD integration via MediatR.

### Minimal APIs (`--apiStyle minimal`)

ASP.NET Core Minimal APIs. Endpoints are grouped in `MinimalApiEndpoints/MinimalApiEndpointRegistration.cs` using an extension method pattern. Add new endpoint groups by registering them in `MapMinimalApiEndpoints()`.

### FastEndpoints (`--apiStyle fastendpoints`)

Uses the [FastEndpoints](https://fast-endpoints.com/) library. Each endpoint is a self-contained class in the `Endpoints/` folder that inherits from `EndpointWithoutRequest<TResponse>` (or `Endpoint<TRequest, TResponse>` for endpoints with a request model). FastEndpoints auto-discovers all endpoints at startup.

## Project Structure

```
MyApp/
├── MyApp.Api/                  # Entry point — startup, middleware, controllers/endpoints
│   ├── Controllers/            # (controllers style only) MVC controllers
│   ├── MinimalApiEndpoints/    # (minimal style only) minimal API endpoint groups
│   ├── Endpoints/              # (fastendpoints style only) FastEndpoints endpoint classes
│   ├── Filters/                # Exception filter
│   ├── Services/               # CurrentUserService
│   ├── Program.cs              # Application entry point
│   └── StartupHelper.cs        # Service/middleware registration helpers
├── MyApp.Application/          # CQRS commands/queries, MediatR handlers, validators
│   ├── Behaviors/              # MediatR pipeline behaviours
│   └── DependencyInjection.cs
├── MyApp.Domain/               # Domain entities, value objects, domain events
│   └── Models/BaseEntity.cs
├── MyApp.Infrastructure/       # EF Core DbContext, repositories, unit of work
│   └── DataAccess/
└── MyApp.Common/               # Shared models, extensions, enumerations
```

## Configuration

Copy `appsettings.json` and fill in your values:

```json
{
  "DATABASE_CON": "Server=localhost,1433;Database=MyApp;User Id=sa;Password=YourPassword;TrustServerCertificate=True;",
  "AppSettings": {
    "Authority": "",
    "Audience": "/resources",
    "ShowSwagger": true,
    "EnableAutoMigration": true
  },
  "Redis": "localhost:6379"
}
```

## Running Locally

1. Start SQL Server and Redis (Docker Compose example):
   ```bash
   docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Password@123" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
   docker run -p 6379:6379 -d redis
   ```

2. Run the API:
   ```bash
   dotnet run --project MyApp.Api
   ```

3. Open Swagger UI at `https://localhost:7254/swagger`

## Publishing the Template to NuGet

```bash
dotnet pack -o ./nupkg
dotnet nuget push ./nupkg/*.nupkg --source https://api.nuget.org/v3/index.json --api-key YOUR_API_KEY
```
