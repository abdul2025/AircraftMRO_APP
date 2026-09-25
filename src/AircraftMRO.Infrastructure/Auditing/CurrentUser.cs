using System.Security.Claims;
using AircraftMRO.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace AircraftMRO.Infrastructure.Auditing;

internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public string? UserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;

            return user?.Identity?.IsAuthenticated == true
                ? user.FindFirstValue(ClaimTypes.NameIdentifier)
                : null;
        }
    }
}
