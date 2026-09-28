using Template.Common.Models;

namespace Template.Api.Filters;

/// <summary>
/// The single way an unexpected exception becomes a 500 response body.
/// </summary>
/// <remarks>
/// Shared by <c>GlobalExceptionFilter</c> (controllers) and <c>ExceptionResponseMiddleware</c>
/// (Minimal API / FastEndpoints), so a server fault looks the same in every API style. The
/// error code in the message is also on the log entry, which is how a client-reported failure
/// is traced back to its exception.
/// </remarks>
internal static partial class ServerErrorResponse
{
    public static ApiResponse<string> Create(Exception exception, bool isDevelopment, ILogger logger)
    {
        var errorId = Guid.NewGuid().ToString();
        LogUnhandledException(logger, exception, errorId);

        var message = isDevelopment
            ? exception.ToString()
            : "An error occurred please try again";

        return new ApiResponse<string>
        {
            Code = ResponseEnums.ResponseCodes.Fail,
            Message = $"{message} Error Code: {errorId}"
        };
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception {ErrorId}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string errorId);
}
