using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Template.Common.Models;

namespace Template.Api.Controllers
{
    [Authorize]
    public abstract class BaseController : ControllerBase
    {

        protected IActionResult CustomResponse<T>(ApiResponse<T> result)
        {

            switch (result.Code)
            {

                case ResponseEnums.ResponseCodes.Fail:
                    return BadRequest(result);
                case ResponseEnums.ResponseCodes.ValidationError:
                    return BadRequest(result);
                case ResponseEnums.ResponseCodes.NotFound:
                    return NotFound(result);
                default:
                    return Ok(result);
            }
        }

    }
}
