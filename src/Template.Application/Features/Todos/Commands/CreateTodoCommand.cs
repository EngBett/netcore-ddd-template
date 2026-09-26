using Template.Application.Features.Todos.Models;
using Template.Application.Interfaces;
using Template.Common.Models;

namespace Template.Application.Features.Todos.Commands;

// Wolverine needs no marker interface on the message: a plain class is enough, and the
// handler is found by the `<Something>Handler.Handle` convention. Dispatch it with
// IMessageBus.InvokeAsync<ApiResponse<TodoDto>>(command).
public class CreateTodoCommand
{
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
}

public class CreateTodoCommandHandler
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
