using Template.Application.Interfaces;
using System.Security.Claims;
using IdentityModel;
namespace Template.Api.Services
{
    public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
    {
        public string? UserId => httpContextAccessor?.HttpContext?.User?.FindFirstValue(JwtClaimTypes.Id) ?? "";

        public string? Email => httpContextAccessor?.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);

        public string? PhoneNumber => httpContextAccessor?.HttpContext?.User?.FindFirstValue(JwtClaimTypes.PhoneNumber);

        public string? Customer => $"{httpContextAccessor?.HttpContext?.User?.FindFirstValue(ClaimTypes.GivenName)}" +
            $" {httpContextAccessor?.HttpContext?.User?.FindFirstValue(ClaimTypes.Surname)}";

    }
}
