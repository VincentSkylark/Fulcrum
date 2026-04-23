using Fulcrum.Core.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Fulcrum.Auth.Endpoints;

public static class SessionEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/session", (ICurrentUserAccessor currentUser) =>
        {
            if (!currentUser.IsAuthenticated)
                return Results.Unauthorized();

            return Results.Ok(new AuthResponse(currentUser.UserId, currentUser.Email, null));
        });
    }
}
