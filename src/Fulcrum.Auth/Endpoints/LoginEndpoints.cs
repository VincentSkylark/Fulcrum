using Fulcrum.Auth.Clients;
using Fulcrum.Core.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Fulcrum.Auth.Endpoints;

public static partial class LoginEndpoints
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Published UserLoggedInEvent for {KratosIdentityId}")]
    private static partial void LogLoginEvent(ILogger logger, Guid kratosIdentityId);

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            IKratosClient kratos,
            IEventBus eventBus,
            ILogger<LoginLogger> logger,
            CancellationToken ct) =>
        {
            var result = await kratos.SubmitLoginFlowAsync(request.Identifier, request.Password, ct);

            if (!result.IsSuccess || result.Value is null)
                return Results.Problem(statusCode: 401, title: "Login failed", detail: result.Error.Message);

            var @event = new UserLoggedInEvent(
                result.Value.IdentityId,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow);

            await eventBus.PublishAsync(@event, ct);
            LogLoginEvent(logger, result.Value.IdentityId);

            return Results.Ok(new AuthResponse(result.Value.IdentityId, result.Value.Email, result.Value.Username));
        });
    }
}

file sealed class LoginLogger;
