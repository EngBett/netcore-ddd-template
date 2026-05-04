using System.ComponentModel;
using System.Data;
using System.Reflection;
using MediatR;
using Template.Application.Interfaces;
using Template.Domain.Models;
using Template.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Template.Infrastructure.DataAccess;

public class ApplicationContext : DbContext, IApplicationContext
{
    private readonly IMediator _mediator;

    public ApplicationContext(DbContextOptions<ApplicationContext> options, IMediator mediator)
        : base(options)
    {
        _mediator = mediator;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var n = await base.SaveChangesAsync(cancellationToken);
        await _mediator.DispatchDomainEventsAsync(this);
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
        return Convert.ToInt32(scalar);
    }
}
