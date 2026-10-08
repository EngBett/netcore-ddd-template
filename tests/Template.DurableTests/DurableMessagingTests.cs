using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Template.DurableTests;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Durable")]
public class DurableMessagingTests(PostgresFixture postgres)
{
    private static string NewId() => Guid.NewGuid().ToString("N");

    private async Task<(IHost Host, string ConnectionString)> StartAsync()
    {
        var cs = await postgres.CreateDatabaseAsync();
        var host = DurableHost.Build(cs);
        await host.StartAsync();
        await DurableHost.CreateWidgetTableAsync(host);
        return (host, cs);
    }

    [Fact]
    public async Task A_handler_that_throws_after_SaveChanges_leaves_no_row_and_no_event()
    {
        var (host, cs) = await StartAsync();
        using var _ = host;
        var id = NewId();

        // The handler takes IApplicationContext and saves itself. Without the DbContext
        // abstraction mapping the transaction middleware never applies to it, the SaveChanges
        // commits on its own and this row survives the exception.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.InvokeMessageAndWaitAsync(new CreateWidget(id, RaiseCreated: true, RaiseArchived: false, ThrowAfterSave: true)));

        Assert.Equal(0, await DurableHost.CountAsync(cs, id));
        Assert.Equal(0, WidgetCreatedHandler.CountFor(id));
    }

    [Fact]
    public async Task A_successful_handler_commits_the_row_and_runs_the_event_handler_exactly_once()
    {
        var (host, cs) = await StartAsync();
        using var _ = host;
        var id = NewId();

        await host.InvokeMessageAndWaitAsync(new CreateWidget(id, RaiseCreated: true, RaiseArchived: false, ThrowAfterSave: false));

        Assert.Equal(1, await DurableHost.CountAsync(cs, id));
        // Two would mean ApplicationContext dispatched inline as well as Wolverine publishing
        // from the outbox.
        Assert.Equal(1, WidgetCreatedHandler.CountFor(id));
    }

    [Fact]
    public async Task A_domain_event_with_no_handler_does_not_fail_the_commit()
    {
        var (host, cs) = await StartAsync();
        using var _ = host;
        var id = NewId();

        await host.InvokeMessageAndWaitAsync(new CreateWidget(id, RaiseCreated: false, RaiseArchived: true, ThrowAfterSave: false));

        Assert.Equal(1, await DurableHost.CountAsync(cs, id));
    }

    [Fact]
    public async Task The_host_refuses_to_start_when_storage_is_missing_and_auto_build_is_off()
    {
        var cs = await postgres.CreateDatabaseAsync();
        using var host = DurableHost.Build(cs, autoBuildStorage: false);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => host.StartAsync());

        Assert.Contains("storage", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_role_without_DDL_rights_runs_against_a_prebuilt_schema()
    {
        var adminCs = await postgres.CreateDatabaseAsync();

        // The deliberate setup step: what the migrator role would run.
        using (var setup = DurableHost.Build(adminCs, autoBuildStorage: true))
        {
            await setup.StartAsync();
            await DurableHost.CreateWidgetTableAsync(setup);
            await setup.StopAsync();
        }

        var role = "app_" + NewId()[..8];
        var database = new Npgsql.NpgsqlConnectionStringBuilder(adminCs).Database!;
        await postgres.ExecuteAsync($"CREATE ROLE \"{role}\" LOGIN PASSWORD 'pw'");
        await postgres.ExecuteAsync($"GRANT CONNECT ON DATABASE \"{database}\" TO \"{role}\"");
        await postgres.ExecuteAsync(
            $"GRANT USAGE ON SCHEMA public, wolverine TO \"{role}\"; "
            + $"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public, wolverine TO \"{role}\"; "
            + $"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public, wolverine TO \"{role}\"; "
            + "REVOKE CREATE ON SCHEMA public FROM PUBLIC",
            database: adminCs);

        var appCs = new Npgsql.NpgsqlConnectionStringBuilder(adminCs) { Username = role, Password = "pw" }.ConnectionString;
        using var app = DurableHost.Build(appCs, autoBuildStorage: false);
        await app.StartAsync();
        var id = NewId();

        await app.InvokeMessageAndWaitAsync(new CreateWidget(id, RaiseCreated: true, RaiseArchived: false, ThrowAfterSave: false));

        Assert.Equal(1, await DurableHost.CountAsync(appCs, id));
        Assert.Equal(1, WidgetCreatedHandler.CountFor(id));
    }

    [Fact]
    public async Task Auto_building_storage_outside_Development_is_rejected()
    {
        var cs = await postgres.CreateDatabaseAsync();

        var error = Assert.Throws<InvalidOperationException>(() =>
            DurableHost.Build(cs, autoBuildStorage: true, environment: "Production"));

        Assert.Contains("DurableMessagingOptions:AutoBuildStorage", error.Message);
    }

    [Fact]
    public async Task The_host_refuses_to_start_when_durable_messaging_is_enabled_but_never_wired_into_Wolverine()
    {
        var cs = await postgres.CreateDatabaseAsync();
        using var host = DurableHost.Build(cs, wireWolverine: false);

        var error = await Assert.ThrowsAnyAsync<Exception>(() => host.StartAsync());

        Assert.Contains("AddDurableMessaging", error.ToString());
    }

    [Fact]
    public async Task An_event_committed_but_not_yet_handled_is_handled_by_the_next_host()
    {
        var cs = await postgres.CreateDatabaseAsync();
        var id = NewId();

        // First host: the row commits and its event is stored, then the event handler parks,
        // so the event is in flight when the host stops.
        WidgetCreatedHandler.Blocked[id] = true;
        using (var first = DurableHost.Build(cs))
        {
            await first.StartAsync();
            await DurableHost.CreateWidgetTableAsync(first);
            await using (var scope = first.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<IMessageBus>()
                    .InvokeAsync(new CreateWidget(id, RaiseCreated: true, RaiseArchived: false, ThrowAfterSave: false));
            }

            Assert.Equal(1, await DurableHost.CountAsync(cs, id));
            Assert.Equal(0, WidgetCreatedHandler.CountFor(id));
            await first.StopAsync(TimeSpan.FromSeconds(10));
        }

        // Second host, same database: nothing re-sends the command. The event is only in the
        // inbox, and it has to be picked up from there.
        WidgetCreatedHandler.Blocked.TryRemove(id, out _);
        using var second = DurableHost.Build(cs, autoBuildStorage: false);
        await second.StartAsync();

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (WidgetCreatedHandler.CountFor(id) == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(500);

        var diagnostics = string.Join(" | ", await new SagaQueries(cs).RowsAsync(
            "select 'incoming ' || status || ' owner=' || owner_id || ' attempts=' || attempts from wolverine.wolverine_incoming_envelopes "
            + "union all select 'dead ' || coalesce(exception_type,'?') from wolverine.wolverine_dead_letters "
            + "union all select 'outgoing ' || owner_id from wolverine.wolverine_outgoing_envelopes "
            + "union all select 'node ' || node_number || ' ' || coalesce(description,'') from wolverine.wolverine_nodes"));
        Assert.True(WidgetCreatedHandler.CountFor(id) == 1, diagnostics);
    }
}
