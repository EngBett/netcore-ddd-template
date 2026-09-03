using System.Globalization;
using Template.Api;
using Template.Application;
using Template.Infrastructure;
using Template.Common.Options;
using Template.Api.MinimalApiEndpoints;
using Template.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.ConfigureAppConfiguration((hostingContext, config) =>
{
    var env = hostingContext.HostingEnvironment;
    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
    config.AddEnvironmentVariables();
});

// Add services to the container.
builder.Services.AddApiDependencies(builder.Configuration);
builder.Services.AddApplicationDependencies(builder.Configuration);
builder.Services.AddInfrastructureDependencies(builder.Configuration);
// Seq comes from ApplicationOptions.LogUrl. Hardcoding it means logs silently
// go nowhere in every environment except a developer's laptop.
builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
      .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);

    var logUrl = ctx.Configuration[$"{nameof(ApplicationOptions)}:{nameof(ApplicationOptions.LogUrl)}"];
    if (!string.IsNullOrWhiteSpace(logUrl))
        lc.WriteTo.Seq(logUrl);
});

var app = builder.Build();

// Auto-migration is a development convenience. ApplicationOptions.EnableAutoMigration
// exists to control it, so honour it — migrating unconditionally on every start
// means a deploy can silently reshape a production schema. Outside Development
// this is a hard failure rather than a silent override: migrations there should be
// a deliberate, reviewable step with a rollback plan.
var applicationOptions = app.Configuration
    .GetSection(nameof(ApplicationOptions))
    .Get<ApplicationOptions>() ?? new ApplicationOptions();

if (applicationOptions.EnableAutoMigration)
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "ApplicationOptions.EnableAutoMigration is true outside Development. "
            + "Run migrations as a deliberate step instead, and set this to false.");
    }

    using var scope = app.Services.CreateScope();
    var ctx = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
    if (ctx.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        await ctx.Database.MigrateAsync();
}

app.ConfigureMiddleware();

// Minimal API endpoints
app.MapMinimalApiEndpoints();

app.Run();
