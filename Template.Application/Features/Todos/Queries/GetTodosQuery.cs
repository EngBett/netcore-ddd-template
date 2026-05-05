using MediatR;
using Template.Application.Features.Todos.Models;
using Template.Application.Interfaces;
using Template.Common.Models;

namespace Template.Application.Features.Todos.Queries;

public class GetTodosQuery : IRequest<ApiResponse<IEnumerable<TodoDto>>>
{
    public string UserId { get; set; } = null!;
}

public class GetTodosQueryHandler : IRequestHandler<GetTodosQuery, ApiResponse<IEnumerable<TodoDto>>>
{
    private readonly IApplicationContext _db;

    public GetTodosQueryHandler(IApplicationContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<IEnumerable<TodoDto>>> Handle(GetTodosQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_db);
        await Task.CompletedTask;
        return ResponseMessage.Success<IEnumerable<TodoDto>>(new List<TodoDto>());
    }
}