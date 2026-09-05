using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
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
using System.Net;
using System.Text.RegularExpressions;
using Template.Common.Models;
using Template.Domain.Exceptions;

namespace Template.Api.Filters
{
    public class GlobalExceptionFilter : IExceptionFilter
    {
        private readonly IHostEnvironment env;
        private readonly ILogger<GlobalExceptionFilter> _logger;


        public GlobalExceptionFilter(IHostEnvironment env, ILogger<GlobalExceptionFilter> logger)
        {
            this.env = env;
            _logger = logger;
        }

        public void OnException(ExceptionContext context)
        {

            var logKey = Guid.NewGuid().ToString();
            ApiResponse<string> apiResponse = new ApiResponse<string>()
            {
                Code = ResponseEnums.ResponseCodes.Fail,
                Result = null
            };
            if (context.Exception.GetType() == typeof(DomainException))
            {
                apiResponse.Message = context.Exception.Message.ToString();
                context.HttpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;

                context.Result = new BadRequestObjectResult(apiResponse);
            }
            else if (context.Exception is DbUpdateException dbUpdateException && TryMapUniqueViolation(dbUpdateException, out var uniqueMessage))
            {
                apiResponse.Message = uniqueMessage ?? "A unique constraint was violated.";
                context.HttpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;

                context.Result = new BadRequestObjectResult(apiResponse);
            }
            else if (context.Exception.GetType() == typeof(DbUpdateException))
            {
                var dbUpdateEx = context.Exception as DbUpdateException;
                apiResponse.Message = $"An error occurred,please try again Error Code: {logKey}";
                LogError(context, logKey);
                context.Result = new BadRequestObjectResult(apiResponse);
            }
            else if (!context.ModelState.IsValid)
            {
                HandleInvalidModelStateException(context, apiResponse);
            }
            else
            {
                apiResponse.Message = "An error occurred please try again";
                if (env.IsDevelopment())
                {
                    apiResponse.Message = context.Exception.ToString();
                }
                // Result asigned to a result object but in destiny the response is empty. It was a known bug of .net core 1.1

                context.Result = new BadRequestObjectResult(apiResponse);
                context.HttpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                apiResponse.Message = $"{apiResponse.Message} Error Code: {logKey}";
                LogError(context, logKey);
            }

            context.ExceptionHandled = true;
        }


        private void LogError(ExceptionContext context, string logKey)
        {
            _logger.LogError(context.Exception, "Unhandled exception {ErrorId}", logKey);
        }
        private static void HandleInvalidModelStateException(ExceptionContext context, ApiResponse<string> apiResponse)
        {
            var details = new ValidationProblemDetails(context.ModelState)
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            };

            context.Result = new BadRequestObjectResult(details);

            context.ExceptionHandled = true;
            var response = new ApiResponse<IEnumerable<string>>
            {
                Code = ResponseEnums.ResponseCodes.ValidationError
            };

            var message = context.ModelState.Values.SelectMany(a => a.Errors).Select(e => e.ErrorMessage);
            var lst = new List<string>();
            lst.AddRange(message);
            response.Message = lst.FirstOrDefault();
            response.Errors = lst;
            context.Result = new BadRequestObjectResult(response);
        }

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
        public static string? UniqueErrorFormatter(SqlException ex, IReadOnlyList<EntityEntry> entitiesNotSaved)
        {
            var message = ex.Errors[0].Message;
            var matches = UniqueConstraintRegex.Matches(message);

            if (matches.Count == 0)
                return null;

            var entityDisplayName = entitiesNotSaved.Count == 1
                ? entitiesNotSaved.Single().Entity.GetType().Name
                : matches[0].Groups[1].Value;

            var returnError = " " +
                              matches[0].Groups[2].Value + " in " +
                              entityDisplayName + ".";
            returnError = $"{entityDisplayName} with matching {matches[0].Groups[2].Value} already exists";
            return returnError;
        }

        //#endif

        private static readonly Regex UniqueConstraintRegex =
            new Regex("IX_([a-zA-Z0-9]*)_([a-zA-Z0-9]*)'", RegexOptions.Compiled);
    }
}
