using System.ComponentModel;
using System.Reflection;
using MediatR;
using Template.Application.Interfaces;
using Template.Domain.Models;
using Template.Infrastructure.Extensions;
using Microsoft.Data.SqlClient;
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
        SqlParameter result = new SqlParameter("@result", System.Data.SqlDbType.Int)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        var sequenceIdentifier = sequence.GetType()
            .GetMember(sequence.ToString())
            .First()
            .GetCustomAttribute<DescriptionAttribute>()
            ?.Description;
        await Database.ExecuteSqlRawAsync($"SELECT @result = (NEXT VALUE FOR [{sequenceIdentifier}])", result);
        return (int)result.Value;
    }
}
