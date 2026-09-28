using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Reqnroll;
using Template.Application.Features.Todos.Queries;
using Template.Application.Interfaces;
using Template.Infrastructure.DataAccess;
using Wolverine;

namespace Template.Tests.Support;

/// <summary>
/// Boots the real API — its own <c>Program</c>, whichever API style it was scaffolded with —
/// in memory, for scenarios that assert what a client sees over HTTP.
/// </summary>
/// <remarks>
/// Substitutes the same two things as <see cref="TestHost"/> — external transports disabled,
/// SQLite in memory — plus two that only an HTTP host needs:
/// <list type="bullet">
///   <item>auto-migration is off, since the provider in appsettings.json is not running;</item>
///   <item><see cref="QueryFaults.Middleware"/> wraps the sample query, so a scenario can make
///   it fail in a chosen way.</item>
/// </list>
/// Everything else, including the exception filter, the client-error middleware and the
/// response mapping, is the code the service ships.
/// </remarks>
[Binding]
public static class ApiHost
{
    private static readonly Lazy<Factory> Instance = new(() => new Factory());

    public static HttpClient CreateClient() => Instance.Value.CreateClient();

    [AfterTestRun]
    public static async Task StopAsync()
    {
        if (Instance.IsValueCreated)
            await Instance.Value.DisposeAsync();
    }

    private sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _connection.Open();

            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ApplicationOptions:EnableAutoMigration"] = "false",
                    // No Seq in a test run; leaving it set only produces failed batch posts.
                    ["ApplicationOptions:LogUrl"] = ""
                }));

            builder.ConfigureTestServices(services =>
            {
                services.DisableAllExternalWolverineTransports();
                services.ConfigureWolverine(options => options.Policies.AddMiddleware(
                    typeof(QueryFaults.Middleware),
                    chain => chain.MessageType == typeof(GetTodosQuery)));

                services.AddScoped(provider => new TestApplicationContext(
                    new DbContextOptionsBuilder<ApplicationContext>().UseSqlite(_connection).Options,
                    provider.GetRequiredService<IMessageBus>(),
                    provider.GetRequiredService<ILogger<ApplicationContext>>()));
                services.AddScoped<IApplicationContext>(provider =>
                    provider.GetRequiredService<TestApplicationContext>());
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                _connection.Dispose();
        }
    }
}
