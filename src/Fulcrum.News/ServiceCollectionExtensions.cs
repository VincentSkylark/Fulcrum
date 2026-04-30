using Fulcrum.News.Clients;
using Fulcrum.News.Clients.GNews;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fulcrum.News;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFulcrumNews(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GNewsOptions>(configuration.GetSection(GNewsOptions.SectionName));

        services.AddHttpClient<INewsProviderClient, GNewsClient>(client =>
        {
            var options = configuration.GetSection(GNewsOptions.SectionName).Get<GNewsOptions>() ?? new GNewsOptions();
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = options.Timeout;
        });

        return services;
    }
}
