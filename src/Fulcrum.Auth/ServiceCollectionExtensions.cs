using Fulcrum.Auth.Clients;
using Fulcrum.Auth.Configuration;
using Fulcrum.Auth.Endpoints;
using Fulcrum.Auth.Identity;
using Fulcrum.Auth.Middleware;
using Fulcrum.Core.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fulcrum.Auth;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFulcrumAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KratosOptions>(configuration.GetSection("Kratos"));
        services.AddSingleton<IKratosClient, KratosClient>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        services.AddScoped<SessionMiddleware>();

        return services;
    }

    public static WebApplication MapFulcrumAuthEndpoints(this WebApplication app)
    {
        LoginEndpoints.Map(app);
        RegisterEndpoints.Map(app);
        RecoveryEndpoints.Map(app);
        VerificationEndpoints.Map(app);
        SessionEndpoints.Map(app);
        WebhookEndpoints.Map(app);
        AccountEndpoints.Map(app);
        return app;
    }

    public static WebApplication UseFulcrumAuthMiddleware(this WebApplication app)
    {
        app.UseMiddleware<SessionMiddleware>();
        return app;
    }
}
