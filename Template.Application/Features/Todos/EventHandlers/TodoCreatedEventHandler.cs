using MediatR;
using Template.Domain.DomainEvents.Todos;

namespace Template.Application.Features.Todos.EventHandlers;

public class TodoCreatedEventHandler:INotificationHandler<TodoCreatedEvent>
{
    public async Task Handle(TodoCreatedEvent notification, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }
}