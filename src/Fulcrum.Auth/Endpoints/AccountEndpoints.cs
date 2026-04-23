using Fulcrum.Auth.Clients;
using Fulcrum.Core.Events;
using Fulcrum.Core.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Fulcrum.Auth.Endpoints;

public static partial class AccountEndpoints
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Account deletion requested for {UserId}")]
    private static partial void LogDeleteRequested(this ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Published UserDeletedEvent for {KratosIdentityId}")]
    private static partial void LogEventPublished(this ILogger logger, Guid kratosIdentityId);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/auth/account", async (
            ICurrentUserAccessor currentUser,
            IKratosClient kratos,
            IEventBus eventBus,
            ILogger<AccountLogger> logger,
            CancellationToken ct) =>
        {
            if (!currentUser.IsAuthenticated)
                return Results.Unauthorized();

            LogDeleteRequested(logger, currentUser.UserId);

            var deleteResult = await kratos.DeleteIdentityAsync(currentUser.UserId, ct);
            if (!deleteResult.IsSuccess)
                return Results.Problem(statusCode: 500, title: "Account deletion failed", detail: deleteResult.Error.Message);

            var @event = new UserDeletedEvent(
                currentUser.UserId,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow);

            await eventBus.PublishAsync(@event, ct);
            LogEventPublished(logger, currentUser.UserId);

            return Results.Ok();
        });
    }
}

file sealed class AccountLogger;
