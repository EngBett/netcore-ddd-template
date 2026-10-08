using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Template.Application;
using Template.Infrastructure;
using Wolverine;

namespace Template.DurableTests;

public static class DurableHost
{
    /// <summary>
    /// Boots the real composition: <c>AddApplicationDependencies</c> and the same
    /// <c>AddApplicationContext</c> the service uses, against a real PostgreSQL. Only the
    /// RabbitMQ transport is switched off.
    /// </summary>
    public static IHost Build(
        string connectionString,
        bool autoBuildStorage = true,
        string environment = "Development",
        bool wireWolverine = true)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DATABASE_CON"] = connectionString,
            ["DatabaseKind"] = "postgres",
            ["ASPNETCORE_ENVIRONMENT"] = environment,
            ["DurableMessagingOptions:Enabled"] = "true",
            ["DurableMessagingOptions:AutoBuildStorage"] = autoBuildStorage.ToString(),
        });

        builder.Services.AddApplicationDependencies(
            builder.Configuration,
            opts =>
            {
                if (wireWolverine)
                    opts.AddDurableMessaging<WidgetContext>(builder.Configuration);
            });
        builder.Services.DisableAllExternalWolverineTransports();
        builder.Services.AddApplicationContext<WidgetContext>(builder.Configuration);
        builder.Services.ConfigureWolverine(o => o.Discovery.IncludeAssembly(typeof(DurableHost).Assembly));

        return builder.Build();
    }

    public static async Task CreateWidgetTableAsync(IHost host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WidgetContext>();

        // EnsureCreated is a no-op once the database holds any table, and Wolverine has already
        // built its own by now, so run the model's own DDL instead.
        await context.Database.ExecuteSqlRawAsync(context.Database.GenerateCreateScript());
    }

    public static async Task<long> CountAsync(string connectionString, string id)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select count(*) from \"Widgets\" where \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", id);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
