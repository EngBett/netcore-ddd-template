using Template.Domain.DomainEvents;
using Template.Domain.DomainEvents.Todos;

namespace Template.Tests.Support;

/// <summary>A domain event the service has no handler for.</summary>
public class UnhandledTestEvent : IDomainEvent;

/// <summary>
/// A second handler for the service's own domain event. Wolverine calls every handler it
/// finds for a message, so this records that dispatch really reached the concrete type
/// without having to change the service's handler to make it observable.
/// </summary>
public class SpyTodoCreatedHandler
{
    public static int Invocations;

    public static void Reset() => Interlocked.Exchange(ref Invocations, 0);

    public Task Handle(TodoCreatedEvent notification)
    {
        Interlocked.Increment(ref Invocations);
        return Task.CompletedTask;
    }
}
