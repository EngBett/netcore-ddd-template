using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Template.Application.Interfaces;
using Template.Common.Options;
using Template.Domain.DomainEvents;
using Template.Domain.Models;
using Template.Infrastructure.DataAccess;
using Template.Infrastructure.DataAccess.Extension;
using Wolverine;

namespace Template.DurableTests;

public class Widget : BaseEntity
{
    public string Name { get; set; } = "";
}

public record WidgetCreated(string WidgetId) : IDomainEvent;

/// <summary>Raised but deliberately never handled.</summary>
public record WidgetArchived(string WidgetId) : IDomainEvent;

public class WidgetContext(
    DbContextOptions<WidgetContext> options,
    IMessageBus bus,
    ILogger<ApplicationContext> logger,
    IOptions<DurableMessagingOptions> durable) : ApplicationContext(options, bus, logger, durable)
{
    public DbSet<Widget> Widgets => Set<Widget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Widget>().HasKey(w => w.Id);

        // The saga is mapped here because this assembly's IEntityTypeConfiguration is not in
        // the one ApplyConfigurationsFromAssembly scans; a service would put it in
        // DataAccess/EntityConfigurations and call ConfigureSaga from the configuration.
        modelBuilder.ApplyConfiguration(new OrderSagaConfiguration());
        modelBuilder.ConfigureSaga<OrderSaga>();
    }
}

public record CreateWidget(string Id, bool RaiseCreated, bool RaiseArchived, bool ThrowAfterSave);

/// <summary>Written the way a service handler is: against IApplicationContext, saving itself.</summary>
public static class CreateWidgetHandler
{
    public static async Task Handle(CreateWidget command, IApplicationContext db)
    {
        var widget = new Widget { Id = command.Id, Name = "w" };
        if (command.RaiseCreated) widget.AddDomainEvent(new WidgetCreated(command.Id));
        if (command.RaiseArchived) widget.AddDomainEvent(new WidgetArchived(command.Id));

        db.Set<Widget>().Add(widget);
        await db.SaveChangesAsync();

        if (command.ThrowAfterSave) throw new InvalidOperationException("boom after SaveChanges");
    }
}

public static class WidgetCreatedHandler
{
    public static readonly ConcurrentDictionary<string, int> Seen = new();

    /// <summary>Ids whose handler parks until the host stops, to leave an event in flight.</summary>
    public static readonly ConcurrentDictionary<string, bool> Blocked = new();

    public static async Task Handle(WidgetCreated created, CancellationToken cancellation)
    {
        if (Blocked.ContainsKey(created.WidgetId))
            await Task.Delay(Timeout.Infinite, cancellation);

        Seen.AddOrUpdate(created.WidgetId, 1, (_, n) => n + 1);
    }

    public static int CountFor(string id) => Seen.TryGetValue(id, out var n) ? n : 0;
}
