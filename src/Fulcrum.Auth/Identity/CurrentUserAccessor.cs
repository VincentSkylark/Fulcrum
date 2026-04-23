using System.Security.Claims;
using Fulcrum.Core.Identity;
using Microsoft.AspNetCore.Http;

namespace Fulcrum.Auth.Identity;

internal sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public Guid UserId
    {
        get
        {
            var claim = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier);
            return Guid.TryParse(claim?.Value, out var id) ? id : Guid.Empty;
        }
    }

    public string Email => httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;

    public string Tier => httpContextAccessor.HttpContext?.User.FindFirst("tier")?.Value ?? "free";

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated is true;
}
