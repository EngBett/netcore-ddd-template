using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Wolverine;
using Wolverine.Tracking;

namespace Template.DurableTests;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Durable")]
public class SagaTests(PostgresFixture postgres)
{
    private async Task<(IHost Host, string ConnectionString)> StartAsync()
    {
        var cs = await postgres.CreateDatabaseAsync();
        var host = DurableHost.Build(cs);
        await host.StartAsync();
        await DurableHost.CreateWidgetTableAsync(host);
        return (host, cs);
    }

    private static async Task<List<string>> QueryAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(reader.GetValue(0).ToString()!);
        return rows;
    }

    [Fact]
    public async Task A_saga_is_stored_and_updated_through_EF_Core()
    {
        var (host, cs) = await StartAsync();
        using var _ = host;
        var id = Guid.NewGuid();

        await host.InvokeMessageAndWaitAsync(new StartOrder(id));
        await host.InvokeMessageAndWaitAsync(new PayOrder(id));

        var row = await QueryAsync(cs, $"select \"State\" || ':' || \"Payments\" from \"OrderSaga\" where \"Id\" = '{id}'");
        Assert.Equal(["Paid:1"], row);
    }

    [Fact]
    public async Task A_message_for_a_saga_that_does_not_exist_is_handled_by_NotFound_not_thrown()
    {
        var (host, _) = await StartAsync();
        using var _ = host;
        var missing = Guid.NewGuid();

        await host.InvokeMessageAndWaitAsync(new PayOrder(missing));

        Assert.Contains(missing, OrderSaga.NotFoundCalls);
    }

    [Fact]
    public async Task Concurrent_updates_to_one_saga_are_never_silently_lost()
    {
        var (host, cs) = await StartAsync();
        using var _ = host;
        var id = Guid.NewGuid();
        await host.InvokeMessageAndWaitAsync(new StartOrder(id));

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            try
            {
                await using var scope = host.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(new PayOrder(id));
                return "ok";
            }
            catch (Exception e)
            {
                return e.GetType().Name;
            }
        }));

        var succeeded = outcomes.Count(outcome => outcome == "ok");
        var stored = int.Parse((await QueryAsync(cs, $"select \"Payments\" from \"OrderSaga\" where \"Id\" = '{id}'")).Single());

        // Every update that reported success must be in the stored state. Without IRevisioned
        // and the concurrency token, this is where it breaks: eight "ok", two stored.
        Assert.Equal(succeeded, stored);
        Assert.All(outcomes.Where(outcome => outcome != "ok"), outcome => Assert.Contains("Concurrency", outcome));
    }

    [Fact]
    public async Task A_timeout_completes_the_saga_and_removes_its_row()
    {
        var (host, cs) = await StartAsync();
        using var _ = host;
        var id = Guid.NewGuid();

        await host.InvokeMessageAndWaitAsync(new StartOrder(id));
        Assert.Equal(["1"], await QueryAsync(cs, $"select count(*) from \"OrderSaga\" where \"Id\" = '{id}'"));

        // The timeout is a durable scheduled message (one second). Poll rather than sleep.
        var deadline = DateTime.UtcNow.AddSeconds(30);
        var remaining = "1";
        while (remaining != "0" && DateTime.UtcNow < deadline)
        {
            await Task.Delay(500);
            remaining = (await QueryAsync(cs, $"select count(*) from \"OrderSaga\" where \"Id\" = '{id}'")).Single();
        }

        Assert.Equal("0", remaining);
    }
}
