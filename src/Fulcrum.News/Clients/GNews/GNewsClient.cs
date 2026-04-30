using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fulcrum.Core.Errors;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Fulcrum.News.Clients.GNews;

public sealed class GNewsClient : INewsProviderClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<GNewsOptions> _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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

        if (!string.IsNullOrWhiteSpace(request.Category))
            queryParams["category"] = request.Category;
        if (!string.IsNullOrWhiteSpace(request.SearchIn))
            queryParams["in"] = request.SearchIn;
        if (!string.IsNullOrWhiteSpace(request.SortBy))
            queryParams["sortby"] = request.SortBy;
        if (request.From is not null)
            queryParams["from"] = FormatDateTime(request.From.Value);
        if (request.To is not null)
            queryParams["to"] = FormatDateTime(request.To.Value);

        return await SendAsync("top-headlines", queryParams, ct);
    }

    public async Task<Result<NewsProviderResponse>> SearchAsync(
        SearchRequest request, CancellationToken ct)
    {
        var queryParams = BuildCommonParams(
            request.Language, request.Country, request.Max,
            request.Page, request.ExpandContent, request.NullableFields);

        queryParams["q"] = request.Query;
        if (!string.IsNullOrWhiteSpace(request.SearchIn))
            queryParams["in"] = request.SearchIn;
        if (!string.IsNullOrWhiteSpace(request.SortBy))
            queryParams["sortby"] = request.SortBy;
        if (request.From is not null)
            queryParams["from"] = FormatDateTime(request.From.Value);
        if (request.To is not null)
            queryParams["to"] = FormatDateTime(request.To.Value);

        return await SendAsync("search", queryParams, ct);
    }

    private async Task<Result<NewsProviderResponse>> SendAsync(
        string endpoint, Dictionary<string, string> queryParams, CancellationToken ct)
    {
        queryParams["apikey"] = _options.Value.ApiKey;

        var url = QueryHelpers.AddQueryString(endpoint, queryParams!);

        using var response = await _httpClient.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
            return GNewsErrorMapper.MapError(response);

        var gnewsResult = await response.Content.ReadFromJsonAsync<GNewsApiResponse>(JsonOptions, ct);

        if (gnewsResult is null)
            return Result<NewsProviderResponse>.Failure(
                Error.Unexpected("deserialization", "Failed to deserialize GNews response"));

        return Result<NewsProviderResponse>.Success(MapResponse(gnewsResult));
    }

    private static Dictionary<string, string> BuildCommonParams(
        string? language, string? country, int? max,
        int? page, bool expandContent, string? nullableFields)
    {
        var parameters = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(language))
            parameters["lang"] = language;
        if (!string.IsNullOrWhiteSpace(country))
            parameters["country"] = country;
        if (max is not null)
            parameters["max"] = max.Value.ToString();
        if (page is not null)
            parameters["page"] = page.Value.ToString();
        if (expandContent)
            parameters["expand"] = "content";
        if (!string.IsNullOrWhiteSpace(nullableFields))
            parameters["nullable"] = nullableFields;

        return parameters;
    }

    private static string FormatDateTime(DateTimeOffset dto) =>
        dto.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

    private static NewsProviderResponse MapResponse(GNewsApiResponse response) => new(
        response.TotalArticles,
        response.Articles.Select(a => new NewsProviderArticle(
            a.Title ?? string.Empty,
            a.Description ?? string.Empty,
            a.Content ?? string.Empty,
            a.Url ?? string.Empty,
            a.Image,
            a.PublishedAt,
            new NewsProviderSource(
                a.Source?.Name ?? string.Empty,
                a.Source?.Url ?? string.Empty))).ToArray());

    private sealed record GNewsApiResponse(
        int TotalArticles,
        GNewsArticle[] Articles);

    private sealed record GNewsArticle(
        string? Title,
        string? Description,
        string? Content,
        string? Url,
        string? Image,
        DateTimeOffset PublishedAt,
        GNewsSource? Source);

    private sealed record GNewsSource(
        string? Name,
        string? Url);
}
