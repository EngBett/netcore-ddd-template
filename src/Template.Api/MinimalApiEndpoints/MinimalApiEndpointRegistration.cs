using Wolverine;
using Template.Api.Extensions;
using Template.Application.Features.Todos.Models;
using Template.Common.Models;
using Template.Application.Features.Todos.Queries;

namespace Template.Api.MinimalApiEndpoints;

/// <summary>
/// Extension method to register all minimal API endpoints.
/// Add your endpoint registrations here.
/// </summary>
public static class MinimalApiEndpointRegistration
{
    public static WebApplication MapMinimalApiEndpoints(this WebApplication app)
    {
        app.MapTestEndpoints();
        // Register additional endpoint groups here
        return app;
    }

    private static void MapTestEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/test").AllowAnonymous();

        group.MapGet("/", async (string userId, IMessageBus bus, CancellationToken cancellationToken) =>
        {
            var query = new GetTodosQuery { UserId = userId };
            var response = await bus.InvokeAsync<ApiResponse<IEnumerable<TodoDto>>>(query, cancellationToken);
            return response.ToHttpResult();
        })
        .WithName("GetTest");
    }
}
