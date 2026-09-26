using Template.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Template.Infrastructure.DataAccess;
using Wolverine;

namespace Template.Infrastructure.Extensions
{
    static class DomainEventDispatcher
    {
        public static async Task DispatchDomainEventsAsync(this IMessageBus bus, ApplicationContext ctx, ILogger logger)
        {
            var domainEntities = ctx.ChangeTracker
                .Entries<BaseEntity>()
                .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Any());

            var domainEvents = domainEntities
                .SelectMany(x => x.Entity.DomainEvents)
                .ToList();

            domainEntities.ToList()
                .ForEach(entity => entity.Entity.ClearDomainEvents());

            foreach (var domainEvent in domainEvents)
            {
                // InvokeAsync dispatches on the runtime type and runs the handlers inline,
                // which is the behaviour MediatR's Publish had here.
                //
                // The guard is not optional. Wolverine treats an event nobody handles as an
                // unroutable message and InvokeAsync throws, which would fail the whole
                // SaveChangesAsync — a missing handler would break the business operation
                // rather than the side effect. Skipping and warning keeps the write
                // succeeding while still making the gap visible.
                if (bus.PreviewSubscriptions(domainEvent).Count == 0)
                {
                    logger.LogWarning(
                        "Domain event {DomainEvent} was raised but no handler is registered for it; skipping.",
                        domainEvent.GetType().Name);
                    continue;
                }

                await bus.InvokeAsync(domainEvent);
            }
        }
    }
}
