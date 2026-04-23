namespace Fulcrum.Auth.Endpoints;

public sealed record WebhookPayload(
    string IdentityId,
    string Email,
    string? Username,
    string? State);
