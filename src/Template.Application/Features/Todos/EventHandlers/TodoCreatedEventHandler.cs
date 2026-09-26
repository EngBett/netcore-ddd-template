using Template.Domain.DomainEvents.Todos;

namespace Template.Application.Features.Todos.EventHandlers;

// Domain-event handlers are ordinary Wolverine handlers. Several classes may handle the
// same event; Wolverine calls them all, which is the fan-out MediatR's notifications gave.
public class TodoCreatedEventHandler
{
    public async Task Handle(TodoCreatedEvent notification, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }
}
