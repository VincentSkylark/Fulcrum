using System.Net;
using Fulcrum.Core.Errors;

namespace Fulcrum.News.Clients.GNews;

internal static class GNewsErrorMapper
{
    public static Error MapError(HttpResponseMessage response) => response.StatusCode switch
    {
        HttpStatusCode.BadRequest => Error.Validation("bad_request", "Invalid request parameters"),
        HttpStatusCode.Unauthorized => Error.Unauthorized("invalid_key", "Invalid or missing API key"),
        HttpStatusCode.Forbidden => Error.Unavailable("daily_quota", "Daily request quota exhausted — stop until 00:00 UTC"),
        HttpStatusCode.TooManyRequests => Error.Unavailable("rate_limit", "Burst rate limit — back off and retry shortly"),
        _ => Error.Unexpected($"http_{(int)response.StatusCode}", $"Unexpected HTTP error: {(int)response.StatusCode}")
    };
}
