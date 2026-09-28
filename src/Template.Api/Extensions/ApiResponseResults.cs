using Template.Common.Models;

namespace Template.Api.Extensions;

/// <summary>
/// Maps an <see cref="ApiResponse{T}"/> to an HTTP result by its <see cref="ApiResponse{T}.Code"/>.
/// </summary>
/// <remarks>
/// The Minimal API and FastEndpoints counterpart of <c>BaseController.CustomResponse</c>, and it
/// must stay in step with it. Returning <c>Results.Ok</c> unconditionally sent a
/// <c>NotFound</c> or <c>Fail</c> response to the client as a 200.
/// </remarks>
public static class ApiResponseResults
{
    public static IResult ToHttpResult<T>(this ApiResponse<T> response) => response.Code switch
    {
        ResponseEnums.ResponseCodes.Fail => Results.BadRequest(response),
        ResponseEnums.ResponseCodes.ValidationError => Results.BadRequest(response),
        ResponseEnums.ResponseCodes.NotFound => Results.NotFound(response),
        _ => Results.Ok(response)
    };
}
