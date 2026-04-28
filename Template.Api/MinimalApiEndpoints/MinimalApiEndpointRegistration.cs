using Template.Common.Models;

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

        group.MapGet("/", () =>
        {
            var response = new ApiResponse<object>
            {
                Message = "Works well",
                Result = new { Msg = "Test works" }
            };
            return Results.Ok(response);
        })
        .WithName("GetTest");
    }
}
