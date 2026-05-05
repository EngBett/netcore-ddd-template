using MediatR;
using Template.Application.Features.Todos.Models;
using Template.Application.Interfaces;
using Template.Common.Models;

namespace Template.Application.Features.Todos.Commands;

public class CreateTodoCommand : IRequest<ApiResponse<TodoDto>>
{
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
}

public class CreateTodoCommandHandler : IRequestHandler<CreateTodoCommand, ApiResponse<TodoDto>>
{
    private readonly IApplicationContext _db;

    public CreateTodoCommandHandler(IApplicationContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<TodoDto>> Handle(CreateTodoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_db);
        await Task.CompletedTask;
        return ResponseMessage.Success(new TodoDto());
    }
}