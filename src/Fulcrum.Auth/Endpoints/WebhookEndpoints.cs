using Fulcrum.Auth.Configuration;
using Fulcrum.Core.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fulcrum.Auth.Endpoints;

public static partial class WebhookEndpoints
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook {Hook} for {IdentityId}")]
    private static partial void LogWebhook(this ILogger logger, string hook, string identityId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook rejected: invalid secret")]
    private static partial void LogWebhookRejected(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Published {EventType} for {KratosIdentityId}")]
    private static partial void LogEventPublished(this ILogger logger, string eventType, Guid kratosIdentityId);

    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth/webhooks");

        group.MapPost("/after-registration", async (
            WebhookPayload payload,
            IEventBus eventBus,
            ILogger<WebhookLogger> logger,
            CancellationToken ct) =>
        {
            logger.LogWebhook("after-registration", payload.IdentityId);
            return await PublishUserRegisteredAsync(payload, eventBus, logger, ct);
        });

        group.MapPost("/after-settings", async (
            WebhookPayload payload,
            IEventBus eventBus,
            ILogger<WebhookLogger> logger,
            CancellationToken ct) =>
        {
            logger.LogWebhook("after-settings", payload.IdentityId);
            return await PublishUserRegisteredAsync(payload, eventBus, logger, ct);
        });

        group.AddEndpointFilter(async (context, next) =>
        {
            var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<KratosOptions>>();
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<WebhookLogger>>();

            if (!context.HttpContext.Request.Headers.TryGetValue("X-Kratos-Webhook-Secret", out var secret) ||
                secret != options.Value.WebhookSecret)
            {
                logger.LogWebhookRejected();
                return Results.Unauthorized();
            }

            return await next(context);
        });
    }

    private static async Task<IResult> PublishUserRegisteredAsync(
        WebhookPayload payload, IEventBus eventBus, ILogger logger, CancellationToken ct)
    {
        if (!Guid.TryParse(payload.IdentityId, out var kratosIdentityId))
            return Results.Problem(statusCode: 400, title: "Invalid identity ID");

        var @event = new UserRegisteredEvent(
            kratosIdentityId,
            payload.Email,
            payload.Username ?? string.Empty,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        await eventBus.PublishAsync(@event, ct);
        logger.LogEventPublished(nameof(UserRegisteredEvent), kratosIdentityId);
        return Results.Ok();
    }
}

file sealed class WebhookLogger;
