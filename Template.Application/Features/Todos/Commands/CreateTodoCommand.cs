using MediatR;
using Template.Application.Features.Todos.Models;
using Template.Application.Interfaces;

namespace Template.Application.Features.Todos.Commands;

public class CreateTodoCommand : IRequest<TodoDto>
{
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
}

public class CreateTodoCommandHandler : IRequestHandler<CreateTodoCommand, TodoDto>
{
    private readonly IApplicationContext _db;

    public CreateTodoCommandHandler(IApplicationContext db)
    {
        _db = db;
    }

    public async Task<TodoDto> Handle(CreateTodoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_db);
        await Task.CompletedTask;
        return new TodoDto();
    }
}