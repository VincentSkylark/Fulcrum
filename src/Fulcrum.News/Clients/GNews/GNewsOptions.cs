namespace Fulcrum.News.Clients.GNews;

public sealed record GNewsOptions
{
    public const string SectionName = "GNews";

    public string ApiKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://gnews.io/api/v4/";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}
