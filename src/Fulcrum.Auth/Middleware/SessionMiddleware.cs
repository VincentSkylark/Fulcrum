using System.Collections.Concurrent;
using System.Security.Claims;
using Fulcrum.Auth.Clients;
using Fulcrum.Auth.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Fulcrum.Auth.Middleware;

internal sealed class SessionMiddleware(IKratosClient kratos, IOptions<KratosOptions> options) : IMiddleware
{
    private static readonly ConcurrentDictionary<string, CachedSession> _cache = new();
    private readonly long _ttlMs = options.Value.SessionCacheTtlSeconds * 1000L;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var token = ExtractSessionToken(context);
        if (token is not null)
        {
            var session = GetCached(token);
            if (session is null)
            {
                var result = await kratos.ValidateSessionAsync(token, context.RequestAborted);
                if (result.IsSuccess && result.Value is not null)
                {
                    session = result.Value;
                    _cache[token] = new CachedSession(session, Environment.TickCount64);
                }
            }

            if (session is not null)
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, session.IdentityId.ToString()),
                    new Claim(ClaimTypes.Email, session.Email),
                    new Claim("tier", "free")
                ], "KratosSession"));
            }
        }

        await next(context);
    }

    private SessionInfo? GetCached(string token)
    {
        if (!_cache.TryGetValue(token, out var cached))
            return null;

        if (Environment.TickCount64 - cached.Tick > _ttlMs)
        {
            _cache.TryRemove(token, out _);
            return null;
        }

        return cached.Session;
    }

    private static string? ExtractSessionToken(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Session-Token", out var token) &&
            !string.IsNullOrWhiteSpace(token))
            return token.ToString();

        if (context.Request.Cookies.TryGetValue("ory_kratos_session", out var cookie))
            return $"ory_kratos_session={cookie}";

        return null;
    }

    private sealed record CachedSession(SessionInfo Session, long Tick);
}
