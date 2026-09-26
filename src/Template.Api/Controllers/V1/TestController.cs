using Wolverine;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Template.Application.Features.Todos.Models;
using Template.Application.Features.Todos.Queries;
using Template.Common.Models;

namespace Template.Api.Controllers.V1;

[AllowAnonymous]
[ApiController]
[Route("api/v1/[controller]")]
public class TestController(IMessageBus bus) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GetTodosQuery query)
        => CustomResponse(await bus.InvokeAsync<ApiResponse<IEnumerable<TodoDto>>>(query));
}