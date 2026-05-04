using MediatR;
using Template.Application.Features.Todos.Models;

namespace Template.Application.Features.Todos.Commands;

public class CreateTodoCommand : IRequest<TodoDto>
{
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
}

public class CreateTodoCommandHandler : IRequestHandler<CreateTodoCommand, TodoDto>
{
    public CreateTodoCommandHandler()
    {
        
    }
    
    public async Task<TodoDto> Handle(CreateTodoCommand request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return new TodoDto();
    }
}