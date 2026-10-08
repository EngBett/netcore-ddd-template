using System.Globalization;
using System.ComponentModel;
using System.Data;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Template.Application.Interfaces;
using Template.Common.Options;
using Template.Domain.Models;
using Template.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Template.Infrastructure.DataAccess;

public class ApplicationContext : DbContext, IApplicationContext
{
    private readonly IMessageBus _bus;
    private readonly ILogger<ApplicationContext> _logger;
    private readonly bool _durableMessaging;

    public ApplicationContext(
        DbContextOptions<ApplicationContext> options,
        IMessageBus bus,
        ILogger<ApplicationContext> logger,
        IOptions<DurableMessagingOptions>? durableMessaging = null)
        : this((DbContextOptions)options, bus, logger, durableMessaging)
    {
    }

    /// <summary>
    /// For a derived context registered with its own <c>DbContextOptions&lt;TContext&gt;</c>,
    /// which cannot be passed to the typed constructor above.
    /// </summary>
    protected ApplicationContext(
        DbContextOptions options,
        IMessageBus bus,
        ILogger<ApplicationContext> logger,
        IOptions<DurableMessagingOptions>? durableMessaging = null)
        : base(options)
    {
        _bus = bus;
        _logger = logger;
        _durableMessaging = durableMessaging?.Value.Enabled ?? false;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Entity and saga mappings live in DataAccess/EntityConfigurations, one
        // IEntityTypeConfiguration<T> each, and are picked up here without being listed.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var n = await base.SaveChangesAsync(cancellationToken);

        // Durable messaging on: publish the events through the bus rather than invoking their
        // handlers inline. Inside a handler's transaction that stores them in the outbox in the
        // same commit, so they are delivered at least once even if the process dies right
        // after. Dispatching inline would run the handlers before the commit stands and lose
        // the event on a crash.
        //
        // Wolverine's own EF Core domain-event scraping is deliberately not used: in testing the
        // events it enqueued were handled but never written to the inbox, so a restart lost
        // them, whereas a plain PublishAsync was stored and survived.
        if (_durableMessaging)
            await _bus.PublishDomainEventsAsync(this);
        else
            await _bus.DispatchDomainEventsAsync(this, _logger);

        return n;
    }

    public async Task<int> GetNextSequence(DatabaseSequence sequence)
    {
        var sequenceIdentifier = sequence.GetType()
            .GetMember(sequence.ToString())
            .First()
            .GetCustomAttribute<DescriptionAttribute>()
            ?.Description;
        if (string.IsNullOrEmpty(sequenceIdentifier))
            throw new InvalidOperationException(
                $"DatabaseSequence.{sequence} must use [{nameof(DescriptionAttribute)}] with the database sequence name.");

        var connection = Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = Database.ProviderName switch
        {
            "Microsoft.EntityFrameworkCore.SqlServer" => $"SELECT NEXT VALUE FOR [{sequenceIdentifier}]",
            "Npgsql.EntityFrameworkCore.PostgreSQL" => $"SELECT nextval('{sequenceIdentifier}')",
            "Pomelo.EntityFrameworkCore.MySql" => $"SELECT NEXT VALUE FOR `{sequenceIdentifier}`",
            "Microsoft.EntityFrameworkCore.Sqlite" => throw new NotSupportedException(
                "SQLite has no built-in server sequences compatible with this helper; use INTEGER PRIMARY KEY or custom SQL."),
            _ => throw new NotSupportedException($"GetNextSequence is not mapped for provider {Database.ProviderName}.")
        };

        var scalar = await command.ExecuteScalarAsync();
        return Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
    }
}
