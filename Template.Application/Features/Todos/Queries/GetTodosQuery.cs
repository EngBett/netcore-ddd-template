using MediatR;
using Template.Application.Features.Todos.Models;
using Template.Application.Interfaces;

namespace Template.Application.Features.Todos.Queries;

public class GetTodosQuery : IRequest<IEnumerable<TodoDto>>
{
    public string UserId { get; set; } = null!;
}

public class GetTodosQueryHandler : IRequestHandler<GetTodosQuery, IEnumerable<TodoDto>>
{
    private readonly IApplicationContext _db;

    public GetTodosQueryHandler(IApplicationContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<TodoDto>> Handle(GetTodosQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_db);
        await Task.CompletedTask;
        return new List<TodoDto>();
    }
}