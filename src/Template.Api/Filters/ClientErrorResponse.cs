using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
//#if (useMssql)
using Microsoft.Data.SqlClient;
//#endif
//#if (useSqlite)
using Microsoft.Data.Sqlite;
//#endif
//#if (useMysql)
using MySqlConnector;
//#endif
//#if (usePostgres)
using Npgsql;
//#endif
using Template.Common.Models;
using Template.Domain.Exceptions;

namespace Template.Api.Filters;

/// <summary>
/// The single mapping from "an exception the caller caused" to the template's 400 payload.
/// </summary>
/// <remarks>
/// Shared by <c>GlobalExceptionFilter</c> (controllers) and <c>ExceptionResponseMiddleware</c>
/// (Minimal API / FastEndpoints), so a given failure produces the same status and body
/// whichever API style the service was scaffolded with. Anything not recognised here is a
/// server fault, which both callers hand to <see cref="ServerErrorResponse"/>.
/// </remarks>
internal static class ClientErrorResponse
{
    public static bool TryMap(Exception exception, out object? body)
    {
        switch (exception)
        {
            case ValidationException validationException:
                body = ValidationFailureResponse.From(validationException);
                return true;

            // `is`, not an exact type match, so a subclass of DomainException is still
            // treated as a business-rule violation rather than falling through to a 500.
            case DomainException domainException:
                body = Fail(domainException.Message);
                return true;

            case DbUpdateException dbUpdateException when TryMapUniqueViolation(dbUpdateException, out var uniqueMessage):
                body = Fail(uniqueMessage ?? "A unique constraint was violated.");
                return true;

            default:
                body = null;
                return false;
        }
    }

    private static ApiResponse<string> Fail(string message) => new()
    {
        Code = ResponseEnums.ResponseCodes.Fail,
        Message = message
    };

    private static bool TryMapUniqueViolation(DbUpdateException dbUpdateEx, out string? message)
    {
        message = null;
        switch (dbUpdateEx.InnerException)
        {
            //#if (useMssql)
            case SqlException { Number: 2627 or 2601 } sql:
                message = UniqueErrorFormatter(sql, dbUpdateEx.Entries);
                return message != null;
            //#endif
            //#if (usePostgres)
            case PostgresException { SqlState: "23505" } pg:
                message = pg.Message;
                return true;
            //#endif
            //#if (useSqlite)
            case SqliteException { SqliteExtendedErrorCode: 2067 } sqlite:
                message = sqlite.Message;
                return true;
            //#endif
            //#if (useMysql)
            case MySqlException { Number: 1062 } my:
                message = my.Message;
                return true;
            //#endif
            default:
                return false;
        }
    }

    //#if (useMssql)
    private static string? UniqueErrorFormatter(SqlException ex, IReadOnlyList<EntityEntry> entitiesNotSaved)
    {
        var message = ex.Errors[0].Message;
        var matches = UniqueConstraintRegex.Matches(message);

        if (matches.Count == 0)
            return null;

        var entityDisplayName = entitiesNotSaved.Count == 1
            ? entitiesNotSaved.Single().Entity.GetType().Name
            : matches[0].Groups[1].Value;

        return $"{entityDisplayName} with matching {matches[0].Groups[2].Value} already exists";
    }

    private static readonly Regex UniqueConstraintRegex =
        new("IX_([a-zA-Z0-9]*)_([a-zA-Z0-9]*)'", RegexOptions.Compiled);
    //#endif
}
