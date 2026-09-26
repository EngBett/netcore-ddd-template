using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Template.Infrastructure.DataAccess;
using Wolverine;

namespace Template.Tests.Support;

/// <summary>
/// An entity that exists only so the suite has something to attach domain events to.
/// </summary>
public class Widget : Template.Domain.Models.BaseEntity
{
    public string Name { get; set; } = "widget";
}

/// <summary>
/// Subclasses the real <see cref="ApplicationContext"/> so that
/// <c>SaveChangesAsync</c> — and therefore domain-event dispatch — is the production code
/// path. Only the model differs: it gains <see cref="Widget"/> to hang events on.
/// </summary>
public class TestApplicationContext(
    DbContextOptions<ApplicationContext> options,
    IMessageBus bus,
    ILogger<ApplicationContext> logger) : ApplicationContext(options, bus, logger)
{
    public DbSet<Widget> Widgets => Set<Widget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Widget>().HasKey(widget => widget.Id);
    }
}
