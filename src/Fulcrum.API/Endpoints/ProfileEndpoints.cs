using Fulcrum.API.Data;
using Fulcrum.Auth.Clients;
using Fulcrum.Core.Errors;
using Fulcrum.Core.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Fulcrum.API.Endpoints;

public static class ProfileEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profile");

        group.MapGet("/", async (
            ICurrentUserAccessor currentUser,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (!currentUser.IsAuthenticated)
                return Results.Unauthorized();

            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.KratosIdentityId == currentUser.UserId, ct);

            return profile is not null
                ? Results.Ok(profile)
                : Results.NotFound();
        });

        group.MapPut("/", async (
            UpdateProfileRequest request,
            ICurrentUserAccessor currentUser,
            AppDbContext db,
            IKratosClient kratos,
            CancellationToken ct) =>
        {
            if (!currentUser.IsAuthenticated)
                return Results.Unauthorized();

            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.KratosIdentityId == currentUser.UserId, ct);

            if (profile is null)
                return Results.NotFound();

            var newEmail = request.Email ?? profile.Email;
            var newUsername = request.Username ?? profile.Username;

            var kratosResult = await kratos.UpdateIdentityTraitsAsync(
                currentUser.UserId, newEmail, newUsername, ct);

            if (!kratosResult.IsSuccess)
                return Results.Problem(statusCode: 502, title: "Failed to sync identity", detail: kratosResult.Error.Message);

            profile.Email = newEmail;
            profile.Username = newUsername;
            profile.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(profile);
        });
    }
}
