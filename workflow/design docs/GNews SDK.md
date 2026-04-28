# GNews SDK — Design Doc

## Purpose

A thin C# HTTP client wrapping the [GNews REST API v4](https://gnews.io) for the `Fulcrum.News` module. The SDK is internal to Fulcrum — not a publishable NuGet package. It exists because there is no official .NET SDK for GNews.

## Scope

### In scope
- `GET /v4/top-headlines` — fetch headlines by category, country, language
- `GET /v4/search` — keyword search with date range and field targeting
- Typed request/response models
- `Result<T>` error handling (consistent with Fulcrum.Core)
- DI registration via `AddGNewsClient()` extension method
- Configuration via `IOptions<GNewsOptions>`

### Out of scope
- Full article content extraction (GNews returns truncated content; full extraction is a separate concern)
- Caching (handled upstream by the ingestion pipeline)
- Webhook/SSE subscriptions (GNews is request/response only)
- Rate limiting enforcement (GNews free tier: 100 requests/day; the ingestion pipeline should throttle, not the SDK)

## GNews API Reference

### Base URL
```
https://gnews.io/api/v4
```

### Authentication
API key passed as `apikey` query parameter on every request.

### Endpoints

#### Top Headlines
```
GET /top-headlines
```

| Parameter   | Type   | Required | Values                                          |
|-------------|--------|----------|-------------------------------------------------|
| `category`  | string | no       | general, world, nation, business, technology, entertainment, sports, science, health |
| `lang`      | string | no       | ISO 639-1 (e.g., `en`, `es`, `fr`)             |
| `country`   | string | no       | ISO 3166-1 alpha-2 (e.g., `us`, `gb`, `de`)    |
| `max`       | int    | no       | 1–10 (default 10)                               |
| `page`      | int    | no       | Page number for pagination                      |
| `expand`    | string | no       | `content` to get full content field             |
| `nullable`  | string | no       | `title`, `description`, `content`, etc.         |

#### Search
```
GET /search
```

| Parameter   | Type   | Required | Values                                          |
|-------------|--------|----------|-------------------------------------------------|
| `q`         | string | **yes**  | Search query (supports AND/OR/NOT operators)    |
| `lang`      | string | no       | ISO 639-1                                       |
| `country`   | string | no       | ISO 3166-1 alpha-2                              |
| `max`       | int    | no       | 1–10 (default 10)                               |
| `page`      | int    | no       | Page number for pagination                      |
| `in`        | string | no       | `title`, `description`, `content` (comma-sep)   |
| `expand`    | string | no       | `content` to get full content field             |
| `nullable`  | string | no       | Include articles with missing fields            |
| `from`      | string | no       | ISO 8601 datetime (e.g., `2026-04-20T00:00:00Z`) |
| `to`        | string | no       | ISO 8601 datetime                               |

### Response Shape (shared by both endpoints)

```json
{
  "totalArticles": 50,
  "articles": [
    {
      "title": "...",
      "description": "...",
      "content": "...",
      "url": "https://...",
      "image": "https://...",
      "publishedAt": "2026-04-27T10:30:00Z",
      "source": {
        "name": "BBC",
        "url": "https://bbc.com"
      }
    }
  ]
}
```

### Error Responses
```json
{
  "errors": [
    {
      "code": "...",
      "message": "..."
    }
  ]
}
```

HTTP status codes: `400` (bad request), `401` (invalid API key), `403` (rate limit exceeded), `429` (too many requests).

## Design Decisions

### 1. Provider abstraction — generic interface (resolved)

Use a generic `INewsProviderClient` interface instead of a GNews-specific one. The GNews client implements this interface. When we add a second provider (NewsAPI, MediaStack, etc.), we register a different implementation in DI — no consumer code changes.

```csharp
// Generic interface — lives in Fulcrum.Core or Fulcrum.News
public interface INewsProviderClient
{
    Task<Result<NewsProviderResponse>> GetTopHeadlinesAsync(
        TopHeadlinesRequest request,
        CancellationToken ct = default);

    Task<Result<NewsProviderResponse>> SearchAsync(
        SearchRequest request,
        CancellationToken ct = default);
}

// GNews-specific implementation
public sealed class GNewsClient : INewsProviderClient { ... }

// DI — swap provider here
services.AddHttpClient<INewsProviderClient, GNewsClient>(...);
```

Response and request records use provider-neutral names (`NewsProviderResponse`, `NewsProviderArticle`). GNews-specific JSON fields map to these in the client implementation.

### 2. File location (resolved)

`Fulcrum.News/Clients/GNews/` — follows the Auth module's `Clients/` pattern. The generic interface lives in `Fulcrum.News/Clients/` (one level up) so future providers sit alongside it.

```
src/Fulcrum.News/
├── Clients/
│   ├── INewsProviderClient.cs       # Generic interface
│   ├── NewsProviderContracts.cs     # Provider-neutral request/response records
│   └── GNews/
│       ├── GNewsClient.cs           # INewsProviderClient implementation
│       ├── GNewsOptions.cs          # GNews-specific config
│       └── GNewsErrorMapper.cs      # Maps GNews HTTP errors to Result errors
```

### 3. `Result<T>` for all outcomes (resolved)

Use `Result<T>` from `Fulcrum.Core` for all API call outcomes:
- `Result<NewsProviderResponse>` on success
- `Result<NewsProviderResponse>` with typed errors on failure (network, auth, rate limit, deserialization)

Expected failures (rate limits, bad keys) are Results, not exceptions. Only truly unexpected states throw.

### 4. `IHttpClientFactory` (resolved)

**`IHttpClientFactory`** — required by project conventions. Registration:

```csharp
services.AddHttpClient<INewsProviderClient, GNewsClient>(client =>
{
    client.BaseAddress = new Uri("https://gnews.io/api/v4/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

### 5. Pagination (resolved)

SDK exposes `page` + `max` parameters but **does not** auto-paginate. The ingestion pipeline controls pagination logic (capping pages, respecting rate limits, parallelizing). The SDK is a dumb transport layer.

### 6. Content expansion — opt-in, no-op on free tier (resolved)

`ExpandContent` is opt-in (default `false`). On the **free tier**, `expand=content` is a paid feature — the parameter is accepted but GNews still returns truncated content. This is fine: the SDK sends the param, GNews ignores it on free plans, and when we upgrade to Essential, full content works without code changes.

### 7. Resilience — caller handles retry (resolved)

**No Polly in the SDK.** Retry, circuit-breaker, and rate-limit backoff are the caller's responsibility. The SDK is a pure transport layer — it sends a request and returns a `Result<T>`. The ingestion pipeline decides whether to retry a 429, back off on 403, or fail fast.

That said, the caller may layer transient-error handling via `AddPolicyHandler()` at DI registration time (e.g., retry 500s/503s once). This is infrastructure-level resilience, not business retry — it lives in the DI wiring, not in the SDK code:

```csharp
// Optional: caller adds transient retry at registration time
services.AddHttpClient<INewsProviderClient, GNewsClient>(...)
    .AddPolicyHandler(Policy.Handle<HttpRequestException>()
        .OrResult<HttpResponseMessage>(r => r.StatusCode is >= HttpStatusCode.InternalServerError)
        .RetryAsync(1));
```

### 8. Source deduplication — caller handles it (resolved)

GNews often returns the same article from multiple sources. The SDK does **not** deduplicate. The ingestion pipeline is responsible for dedup based on URL, title similarity, or content hash.

### 9. Plan — free tier for now (resolved)

Starting on the free tier (100 req/day, 10 articles/request, 12-hour delay, no full content). The SDK is designed so upgrading to Essential requires only a config change (new API key) — no code changes.

### GNews Free Tier Constraints (reference)

| Feature | Free | Essential (€50/mo) | Business (€100/mo) |
|---|---|---|---|
| Requests/day | 100 | 1,000 | 5,000 |
| Articles/request | 10 | 25 | 50 |
| Full content | No | Yes | Yes |
| Article delay | 12 hours | Real-time | Real-time |
| Historical data | 30 days | From 2020 | From 2020 |

## Proposed API Surface

### Generic Interface (provider-neutral)

```csharp
// Clients/INewsProviderClient.cs
public interface INewsProviderClient
{
    Task<Result<NewsProviderResponse>> GetTopHeadlinesAsync(
        TopHeadlinesRequest request,
        CancellationToken ct = default);

    Task<Result<NewsProviderResponse>> SearchAsync(
        SearchRequest request,
        CancellationToken ct = default);
}
```

### Provider-Neutral Contracts

```csharp
// Clients/NewsProviderContracts.cs

// --- Requests ---
public sealed record TopHeadlinesRequest(
    string? Category = null,
    string? Language = null,
    string? Country = null,
    int? Max = null,
    int? Page = null,
    bool ExpandContent = false,
    string? NullableFields = null);     // e.g., "title,description"

public sealed record SearchRequest(
    string Query,
    string? Language = null,
    string? Country = null,
    int? Max = null,
    int? Page = null,
    string? SearchIn = null,            // e.g., "title,description,content"
    bool ExpandContent = false,
    string? NullableFields = null,      // e.g., "title,description"
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);

// --- Responses ---
public sealed record NewsProviderResponse(
    int TotalArticles,
    IReadOnlyList<NewsProviderArticle> Articles);

public sealed record NewsProviderArticle(
    string Title,
    string Description,
    string Content,
    string Url,
    string? Image,
    DateTimeOffset PublishedAt,
    NewsProviderSource Source);

public sealed record NewsProviderSource(
    string Name,
    string Url);
```

### GNews-Specific Configuration

```csharp
// Clients/GNews/GNewsOptions.cs
public sealed record GNewsOptions
{
    public const string SectionName = "GNews";

    public string ApiKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://gnews.io/api/v4/";
}
```

```json
// appsettings.json
{
  "GNews": {
    "ApiKey": "your-api-key-here",
    "BaseUrl": "https://gnews.io/api/v4/"
  }
}
```

### GNews Client Implementation (sketch)

```csharp
// Clients/GNews/GNewsClient.cs
public sealed class GNewsClient : INewsProviderClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<GNewsOptions> _options;

    public GNewsClient(HttpClient httpClient, IOptions<GNewsOptions> options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<Result<NewsProviderResponse>> GetTopHeadlinesAsync(
        TopHeadlinesRequest request, CancellationToken ct)
    {
        var queryParams = BuildCommonParams(
            request.Language, request.Country, request.Max,
            request.Page, request.ExpandContent, request.NullableFields);

        if (request.Category is not null)
            queryParams["category"] = request.Category;

        return await SendAsync("top-headlines", queryParams, ct);
    }

    public async Task<Result<NewsProviderResponse>> SearchAsync(
        SearchRequest request, CancellationToken ct)
    {
        var queryParams = BuildCommonParams(
            request.Language, request.Country, request.Max,
            request.Page, request.ExpandContent, request.NullableFields);

        queryParams["q"] = request.Query;
        if (request.SearchIn is not null) queryParams["in"] = request.SearchIn;
        if (request.NullableFields is not null) queryParams["nullable"] = request.NullableFields;
        if (request.From is not null) queryParams["from"] = request.From.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        if (request.To is not null) queryParams["to"] = request.To.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

        return await SendAsync("search", queryParams, ct);
    }

    private async Task<Result<NewsProviderResponse>> SendAsync(
        string endpoint, Dictionary<string, string> queryParams, CancellationToken ct)
    {
        queryParams["apikey"] = _options.Value.ApiKey;

        // Use QueryHelpers for safe URL encoding (handles &, =, spaces, etc.)
        var url = QueryHelpers.AddQueryString(endpoint, queryParams);

        using var response = await _httpClient.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
            return MapError(response);

        var json = await response.Content.ReadAsStringAsync(ct);
        var gnewsResult = JsonSerializer.Deserialize<GNewsApiResponse>(json, JsonOptions);

        if (gnewsResult is null)
            return Result<NewsProviderResponse>.Failure(
                Error.Unexpected("deserialization", "Failed to deserialize GNews response"));

        // Map GNews-specific JSON to provider-neutral records
        return Result<NewsProviderResponse>.Success(MapResponse(gnewsResult));
    }
}
```

### DI Registration

```csharp
// ServiceCollectionExtensions.cs
public static IServiceCollection AddFulcrumNews(this IServiceCollection services, IConfiguration config)
{
    services.Configure<GNewsOptions>(config.GetSection(GNewsOptions.SectionName));

    services.AddHttpClient<INewsProviderClient, GNewsClient>(client =>
    {
        client.BaseAddress = new Uri("https://gnews.io/api/v4/");
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    return services;
}
```

## Error Mapping

Requires adding `Unavailable` to `Fulcrum.Core.Errors.ErrorType` and a matching factory method on `Error`:

```csharp
// Add to ErrorType enum
Unavailable,

// Add to Error record
public static Error Unavailable(string code, string message) =>
    new(ErrorType.Unavailable, code, message);
```

### HTTP Status → Error Mapping

| GNews HTTP Status | `ErrorType`    | `Error.Code`     | Description                        |
|-------------------|----------------|------------------|------------------------------------|
| 400               | `Validation`   | `bad_request`    | Bad request / invalid params       |
| 401               | `Unauthorized` | `invalid_key`    | Invalid or missing API key         |
| 403               | `Unavailable`  | `daily_quota`    | Daily request quota exhausted — stop until 00:00 UTC |
| 429               | `Unavailable`  | `rate_limit`     | Burst rate limit — back off and retry shortly |
| Network failure   | `Unavailable`  | `network_error`  | Timeout, DNS failure, connection reset |
| Invalid JSON      | `Unexpected`   | `deserialization`| Failed to deserialize response     |

The `Error.Code` field distinguishes between 403 (stop for the day) and 429 (retry after brief wait) without needing separate enum members. The ingestion pipeline switches on `Code` to choose the right recovery strategy.
