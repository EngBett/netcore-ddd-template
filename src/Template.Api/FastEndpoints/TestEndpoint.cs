using FastEndpoints;
using Wolverine;
using Template.Api.Extensions;
using Template.Application.Features.Todos.Models;
using Template.Application.Features.Todos.Queries;
using Template.Common.Models;

namespace Template.Api.FastEndpoints;

public class TestRequest
{
    public string UserId { get; set; } = null!;
}

public class TestEndpoint(IMessageBus bus) : Endpoint<TestRequest, ApiResponse<IEnumerable<TodoDto>>>
{
    public override void Configure()
    {
        Get("/api/v1/test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(TestRequest req, CancellationToken ct)
    {
        var query = new GetTodosQuery { UserId = req.UserId };
        var response = await bus.InvokeAsync<ApiResponse<IEnumerable<TodoDto>>>(query, ct);
        await Send.ResultAsync(response.ToHttpResult());
    }
}
