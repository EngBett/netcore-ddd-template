# Agent instructions — netcore-ddd-template

Concise guidance for AI assistants and automation working in this repository.

## What this repo is

A **.NET 10** solution template using **DDD**, **Clean Architecture**, **CQRS (MediatR)**, **EF Core** (SQL Server, PostgreSQL, SQLite, or MySQL), **Redis** caching, **MassTransit + RabbitMQ**, JWT, Serilog, Prometheus, and optional API styles (MVC, Minimal APIs, FastEndpoints). Projects are instantiated with `dotnet new ddd-template` (see `.template.config/template.json`).

## Architecture rules

- **Dependency direction**: `Template.Api` → `Template.Application` → `Template.Domain`; `Template.Infrastructure` implements interfaces from `Application`; `Domain` has no infrastructure or framework references.
- **CQRS**: New features use **commands/queries** + **handlers** in `Template.Application`, validation via **FluentValidation**, optional domain events on entities in `Template.Domain`.
- **Data access**: Handlers depend on **`IApplicationContext`** (not concrete `DbContext` types from Application).

## Where to change behavior

| Concern | Primary location |
|--------|------------------|
| HTTP pipeline, Swagger, JWT middleware | `Template.Api/DependencyInjection.cs`, `Program.cs` |
| MediatR, MassTransit, consumers | `Template.Application/DependencyInjection.cs` |
| EF Core, Redis, migrations | `Template.Infrastructure/DependencyInjection.cs` |
| Strongly typed app settings | `Template.Common/Options/*.cs` |
| Sample appsettings | `Template.Api/appsettings.json` and provider-specific variants |

## Configuration conventions

- Bind options with **`IOptions<T>`** / **`Configure<T>(section)`** using section names that match the options class (e.g. `RedisOptions`, `MassTransitOptions`, `RabbitMQOptions`, `ApplicationOptions`).
- **Redis**: `RedisOptions` (`ConnectionString`, `InstanceName`); wired in Infrastructure via `RedisCacheOptionsConfigurator` + `AddStackExchangeRedisCache`.
- **Messaging**: `RabbitMQOptions` (host is combined into a **`rabbitmq://` URI** including port and virtual host); `MassTransitOptions` (retries, delayed redelivery, `EnableInMemoryOutbox`). In-memory outbox uses **`AddConfigureEndpointsCallback`** and **`UseInMemoryOutbox(registrationContext)`** when enabled—not obsolete bus-only overloads.
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
