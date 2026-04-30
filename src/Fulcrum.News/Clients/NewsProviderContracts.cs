namespace Fulcrum.News.Clients;

public sealed record TopHeadlinesRequest(
    string? Category = null,
    string? Language = null,
    string? Country = null,
    int? Max = null,
    int? Page = null,
    string? SearchIn = null,
    bool ExpandContent = false,
    string? NullableFields = null,
    string? SortBy = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);

public sealed record SearchRequest(
    string Query,
    string? Language = null,
    string? Country = null,
    int? Max = null,
    int? Page = null,
    string? SearchIn = null,
    bool ExpandContent = false,
    string? NullableFields = null,
    string? SortBy = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);

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
