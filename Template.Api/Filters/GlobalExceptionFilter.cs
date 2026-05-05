using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
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
            _logger.LogError(context.Exception, $"ErrorID={logKey}");
        }
        private void HandleInvalidModelStateException(ExceptionContext context, ApiResponse<string> apiResponse)
        {
            var details = new ValidationProblemDetails(context.ModelState)
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            };

            context.Result = new BadRequestObjectResult(details);

            context.ExceptionHandled = true;
            var response = new ApiResponse<IEnumerable<string>>();

            response.Code = ResponseEnums.ResponseCodes.ValidationError;
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
                case SqlException sql when sql.Number is 2627 or 2601:
                    message = UniqueErrorFormatter(sql, dbUpdateEx.Entries);
                    return message != null;
                case PostgresException pg when pg.SqlState == "23505":
                    message = pg.Message;
                    return true;
                case SqliteException sqlite when sqlite.SqliteExtendedErrorCode == 2067:
                    message = sqlite.Message;
                    return true;
                case MySqlException my when my.Number == 1062:
                    message = my.Message;
                    return true;
                default:
                    return false;
            }
        }

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

        private static readonly Regex UniqueConstraintRegex =
            new Regex("IX_([a-zA-Z0-9]*)_([a-zA-Z0-9]*)'", RegexOptions.Compiled);
    }
}
