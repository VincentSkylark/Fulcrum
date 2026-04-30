using Fulcrum.Core.Errors;

namespace Fulcrum.News.Clients;

public interface INewsProviderClient
{
    Task<Result<NewsProviderResponse>> GetTopHeadlinesAsync(
        TopHeadlinesRequest request,
        CancellationToken ct = default);

    Task<Result<NewsProviderResponse>> SearchAsync(
        SearchRequest request,
        CancellationToken ct = default);
}
