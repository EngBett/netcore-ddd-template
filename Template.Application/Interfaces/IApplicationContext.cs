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
    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<int> GetNextSequence(DatabaseSequence sequence);
}
