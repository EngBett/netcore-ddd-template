using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Template.Common.Models;

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
            // Validation failures, domain rule violations and unique-constraint violations are
            // the caller's fault. This mapping, and the 500 below, are shared with
            // ExceptionResponseMiddleware so the other API styles answer identically.
            if (ClientErrorResponse.TryMap(context.Exception, out var clientError))
            {
                context.Result = new BadRequestObjectResult(clientError);
            }
            else if (!context.ModelState.IsValid)
            {
                HandleInvalidModelStateException(context);
            }
            else
            {
                // The status goes on the result itself. Setting Response.StatusCode and then
                // assigning a BadRequestObjectResult sent a 400, because the result writes its
                // own status when it executes.
                context.Result = new ObjectResult(
                    ServerErrorResponse.Create(context.Exception, env.IsDevelopment(), _logger))
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };
            }

            context.ExceptionHandled = true;
        }


        private static void HandleInvalidModelStateException(ExceptionContext context)
        {
            var message = context.ModelState.Values.SelectMany(a => a.Errors).Select(e => e.ErrorMessage);
            var lst = new List<string>();
            lst.AddRange(message);

            context.Result = new BadRequestObjectResult(new ApiResponse<IEnumerable<string>>
            {
                Code = ResponseEnums.ResponseCodes.ValidationError,
                Message = lst.FirstOrDefault(),
                Errors = lst
            });
        }
    }
}
