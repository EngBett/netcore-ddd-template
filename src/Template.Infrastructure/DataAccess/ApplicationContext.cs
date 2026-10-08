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
    private readonly bool _wolverinePublishesDomainEvents;

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
        _wolverinePublishesDomainEvents = durableMessaging?.Value.Enabled ?? false;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var n = await base.SaveChangesAsync(cancellationToken);

        // With durable messaging on, Wolverine scrapes the entities' domain events from the
        // change tracker inside the handler's transaction and stores them in the outbox, so
        // they are published if and only if this commit stands. Dispatching them here as well
        // would run every handler twice, and would do it after the commit, where a crash
        // loses the event.
        if (!_wolverinePublishesDomainEvents)
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
