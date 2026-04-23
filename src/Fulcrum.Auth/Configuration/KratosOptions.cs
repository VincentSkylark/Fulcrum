namespace Fulcrum.Auth.Configuration;

public sealed record KratosOptions
{
    public string PublicBaseUrl { get; init; } = "http://localhost:4433/";
    public string AdminBaseUrl { get; init; } = "http://localhost:4434/";
    public int SessionCacheTtlSeconds { get; init; } = 30;
    public string WebhookSecret { get; init; } = "dev-webhook-secret";
}
