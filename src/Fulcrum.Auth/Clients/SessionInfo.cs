namespace Fulcrum.Auth.Clients;

public sealed record SessionInfo(
    Guid IdentityId,
    string Email,
    string? Username,
    DateTimeOffset ExpiresAt);
