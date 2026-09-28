using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Template.Domain.Models;

namespace Template.Application.Interfaces;

/// <summary>
/// Application-layer abstraction over the EF Core <c>DbContext</c>. The Infrastructure project
/// provides a single concrete <c>DbContext</c> that implements this interface.
/// </summary>
public interface IApplicationContext
{
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "DbContext.Set<TEntity> implements this member, so it has to keep that name.")]
    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<int> GetNextSequence(DatabaseSequence sequence);
}
