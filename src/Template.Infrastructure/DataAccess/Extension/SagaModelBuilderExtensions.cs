using JasperFx;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Template.Infrastructure.DataAccess.Extension;

public static class SagaModelBuilderExtensions
{
    /// <summary>
    /// Maps a Wolverine saga for EF Core storage, with <c>Version</c> as the concurrency token.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="TSaga"/> must implement <see cref="IRevisioned"/>. Without the
    /// interface and the token, two messages for the same saga that overlap in time both
    /// succeed and one silently overwrites the other: in testing, eight concurrent updates
    /// all reported success and only two took effect. With them, the losers throw
    /// <c>SagaConcurrencyException</c> and Wolverine retries or dead-letters them.
    /// Call it from an <c>IEntityTypeConfiguration&lt;TSaga&gt;</c> in
    /// <c>DataAccess/EntityConfigurations</c>.
    /// </remarks>
    public static void ConfigureSaga<TSaga>(this ModelBuilder modelBuilder)
        where TSaga : Saga, IRevisioned
    {
        modelBuilder.Entity<TSaga>().Property(saga => saga.Version).IsConcurrencyToken();
    }
}
