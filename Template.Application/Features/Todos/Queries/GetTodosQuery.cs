using MediatR;
using Template.Application.Features.Todos.Models;

namespace Template.Application.Features.Todos.Queries;

public class GetTodosQuery : IRequest<IEnumerable<TodoDto>>
{
    public string UserId { get; set; } = null!;
}

public class GetTodosQueryHandler : IRequestHandler<GetTodosQuery, IEnumerable<TodoDto>>
{

    public GetTodosQueryHandler()
    {
        
    }

    public async Task<IEnumerable<TodoDto>> Handle(GetTodosQuery request, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return new List<TodoDto>();
    }
}