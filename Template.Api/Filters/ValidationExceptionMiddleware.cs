using System.Net;
using FluentValidation;
using Template.Common.Models;

namespace Template.Api.Filters;

/// <summary>
/// Turns a <see cref="ValidationException"/> into the template's standard
/// <see cref="ApiResponse{T}"/> validation payload.
/// </summary>
/// <remarks>
/// Validation runs as Wolverine middleware (<c>UseFluentValidation()</c>) in front of the
/// handler, so a failure surfaces as a thrown <see cref="ValidationException"/> rather than
/// as invalid model state. Left unmapped it falls through to the catch-all branch and the
/// caller gets a 500 saying "An error occurred please try again", which hides the field
/// errors the client needs.
///
/// The shape deliberately matches <c>GlobalExceptionFilter.HandleInvalidModelStateException</c>
/// so a validation failure looks the same whether it came from model binding or from
/// FluentValidation.
/// </remarks>
internal static class ValidationFailureResponse
{
    public static ApiResponse<IEnumerable<string>> From(ValidationException exception)
    {
        var errors = exception.Errors
            .Select(failure => failure.ErrorMessage)
            .ToList();

        return new ApiResponse<IEnumerable<string>>
        {
            Code = ResponseEnums.ResponseCodes.ValidationError,
            Message = errors.FirstOrDefault(),
            Errors = errors
        };
    }
}

/// <summary>
/// Maps <see cref="ValidationException"/> to a 400 for the API styles that do not run MVC
/// filters.
/// </summary>
public static class ValidationExceptionMiddleware
{
    /// <summary>
    /// Catches validation failures and nothing else, writing the same 400 payload the MVC
    /// filter produces.
    /// </summary>
    /// <remarks>
    /// <c>GlobalExceptionFilter</c> is an <c>IExceptionFilter</c>, so it only ever runs for
    /// controllers. Minimal APIs and FastEndpoints would otherwise return the framework's bare
    /// 500 for a validation failure, with no <see cref="ApiResponse{T}"/> body at all.
    ///
    /// This is a plain catch rather than <c>AddExceptionHandler</c> + <c>UseExceptionHandler</c>
    /// on purpose. <c>UseExceptionHandler</c> needs a fallback handler for the exceptions its
    /// <c>IExceptionHandler</c>s decline, and an empty fallback makes every *other* exception
    /// produce a 404 and then throw. Rethrowing here leaves non-validation exceptions on
    /// exactly the path they took before: the developer exception page in Development, a 500
    /// otherwise.
    ///
    /// For controllers the filter marks the exception handled first, so this never sees it and
    /// the two cannot both write a response.
    /// </remarks>
    public static IApplicationBuilder UseValidationExceptionHandling(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (ValidationException exception) when (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                await context.Response.WriteAsJsonAsync(ValidationFailureResponse.From(exception));
            }
        });
}
