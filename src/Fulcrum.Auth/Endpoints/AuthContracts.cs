namespace Fulcrum.Auth.Endpoints;

public sealed record LoginRequest(string Identifier, string Password);

public sealed record RegisterRequest(string Email, string Password, string Username);

public sealed record RecoveryRequest(string Email);

public sealed record VerifyRequest(string FlowId, string Code);

public sealed record AuthResponse(Guid UserId, string Email, string? Username);
