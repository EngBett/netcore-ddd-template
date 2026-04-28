using FastEndpoints;
using Template.Common.Models;

namespace Template.Api.Endpoints;

/// <summary>
/// Example FastEndpoints endpoint.
/// Each endpoint is a self-contained class that encapsulates the request, response, and handler logic.
/// See https://fast-endpoints.com/ for full documentation.
/// </summary>
public class TestEndpoint : EndpointWithoutRequest<ApiResponse<object>>
{
    public override void Configure()
    {
        Get("/api/v1/test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(new ApiResponse<object>
        {
            Message = "Works well",
            Result = new { Msg = "Test works" }
        }, ct);
    }
}
