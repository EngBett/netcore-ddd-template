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
/// Gives the API styles that do not run MVC filters the same error responses as controllers.
/// </summary>
public static class ExceptionResponseMiddleware
{
    /// <summary>
    /// Turns any exception that escapes an endpoint into the response
    /// <c>GlobalExceptionFilter</c> would have written: a 400 for what
    /// <see cref="ClientErrorResponse"/> recognises as a client error, otherwise a logged 500
    /// from <see cref="ServerErrorResponse"/>.
    /// </summary>
    /// <remarks>
    /// <c>GlobalExceptionFilter</c> is an <c>IExceptionFilter</c>, so it only ever runs for
    /// controllers. Without this, Minimal APIs and FastEndpoints answered a bad request with a
    /// bare 500, and a server fault with an empty body and no error code to trace it by.
    /// Handling server faults here means the developer exception page no longer appears for
    /// those styles; in Development the 500 message carries the full exception instead, as it
    /// does for controllers.
    ///
    /// This is a plain catch rather than <c>AddExceptionHandler</c> + <c>UseExceptionHandler</c>
    /// on purpose. <c>UseExceptionHandler</c> needs a fallback handler for the exceptions its
    /// <c>IExceptionHandler</c>s decline, and an empty fallback makes every one of them
    /// produce a 404 and then throw.
    ///
    /// Two cases are rethrown untouched: a response that has already started cannot be
    /// replaced, and a request the client aborted is not a server fault worth an error log.
    ///
    /// For controllers the filter marks the exception handled first, so this never sees it and
    /// the two cannot both write a response.
    /// </remarks>
    public static IApplicationBuilder UseExceptionResponses(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception exception) when (!context.Response.HasStarted
                                              && !context.RequestAborted.IsCancellationRequested)
            {
                context.Response.Clear();

                if (ClientErrorResponse.TryMap(exception, out var clientError))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    await context.Response.WriteAsJsonAsync(clientError);
                    return;
                }

                var services = context.RequestServices;
                var logger = services.GetRequiredService<ILoggerFactory>()
                    .CreateLogger(typeof(ExceptionResponseMiddleware));
                var isDevelopment = services.GetRequiredService<IHostEnvironment>().IsDevelopment();

                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                await context.Response.WriteAsJsonAsync(
                    ServerErrorResponse.Create(exception, isDevelopment, logger));
            }
        });
}
