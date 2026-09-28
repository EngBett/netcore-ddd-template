using Template.Domain.DomainEvents.Todos;

namespace Template.Application.Features.Todos.EventHandlers;

// Domain-event handlers are ordinary Wolverine handlers. Several classes may handle the
// same event; Wolverine calls them all, which is the fan-out MediatR's notifications gave.
//
// Named `...Handler`, not `...EventHandler`: that suffix is reserved for delegates (CA1711),
// and Wolverine only needs the `Handler` ending. Static because it holds no state: Wolverine
// injects anything the handler needs as a method parameter, e.g.
// `Handle(TodoCreatedEvent e, IApplicationContext db, CancellationToken ct)`.
public static class TodoCreatedHandler
{
    public static Task Handle(TodoCreatedEvent notification, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
