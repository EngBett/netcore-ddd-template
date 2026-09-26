using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Reqnroll;
using Template.Application;
using Template.Application.Interfaces;
using Template.Infrastructure.DataAccess;
using Wolverine;

namespace Template.Tests.Support;

/// <summary>
/// Boots the service's real composition root once for the whole test run.
/// </summary>
/// <remarks>
/// The suite deliberately exercises <c>AddApplicationDependencies</c> itself rather than a
/// hand-rolled stand-in, so the handler discovery, validation middleware, broker routing and
/// retry scoping under test are the ones the service actually ships.
///
/// Two things are swapped out, and only two:
/// <list type="bullet">
///   <item>external transports are disabled, so no RabbitMQ broker is needed;</item>
///   <item>the database is SQLite in memory.</item>
/// </list>
/// </remarks>
[Binding]
public static class TestHost
{
    private static IHost? _host;
    private static SqliteConnection? _connection;

    public static IHost Host =>
        _host ?? throw new InvalidOperationException("The test host has not been started.");

    /// <summary>Resolves a service in a fresh scope, mirroring a single request.</summary>
    public static IServiceScope CreateScope() => Host.Services.CreateScope();

    [BeforeTestRun]
    public static async Task StartAsync()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();

        // The options classes all have usable defaults, so an empty configuration is enough
        // and the suite stays independent of any appsettings file.
        builder.Configuration.AddInMemoryCollection([]);

        builder.Services.AddApplicationDependencies(builder.Configuration);

        // No broker required: routing is still configured and assertable, but nothing dials out.
        builder.Services.DisableAllExternalWolverineTransports();

        // Additive configuration so handlers defined in this test assembly are discovered
        // alongside the service's own.
        builder.Services.ConfigureWolverine(options =>
            options.Discovery.IncludeAssembly(typeof(TestHost).Assembly));

        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        builder.Services.AddScoped(provider => new TestApplicationContext(
            new DbContextOptionsBuilder<ApplicationContext>().UseSqlite(_connection).Options,
            provider.GetRequiredService<IMessageBus>(),
            provider.GetRequiredService<ILogger<ApplicationContext>>()));
        builder.Services.AddScoped<IApplicationContext>(provider =>
            provider.GetRequiredService<TestApplicationContext>());

        _host = builder.Build();
        await _host.StartAsync();

        using var scope = _host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TestApplicationContext>()
            .Database.EnsureCreatedAsync();
    }

    [AfterTestRun]
    public static async Task StopAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
