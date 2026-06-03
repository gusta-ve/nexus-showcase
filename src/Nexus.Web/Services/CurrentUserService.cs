using System.Security.Claims;
using Nexus.Application.Common.Interfaces;

namespace Nexus.Web.Services;

public class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    public string? UserId => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    public string? UserName => accessor.HttpContext?.User.Identity?.Name;
    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
