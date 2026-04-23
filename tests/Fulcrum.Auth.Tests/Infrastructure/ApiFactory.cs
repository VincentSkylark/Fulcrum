using Xunit;
using Fulcrum.API.Data;
using Fulcrum.API.Handlers;
using Fulcrum.Auth.Clients;
using Fulcrum.Auth.Configuration;
using Fulcrum.Core.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace Fulcrum.Auth.Tests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("fulcrum-test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public MockKratosClient Kratos { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IKratosClient>();
            services.AddSingleton<IKratosClient>(Kratos);

            services.RemoveAll<IEventBus>();
            services.AddScoped<IEventBus, SynchronousEventBus>();

            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_db.GetConnectionString()));

            services.RemoveAll<IConfigureOptions<KratosOptions>>();
            services.AddSingleton<IOptions<KratosOptions>>(
                new OptionsWrapper<KratosOptions>(new KratosOptions { WebhookSecret = "test-secret" }));

            services.AddScoped<UserRegisteredHandler>();
            services.AddScoped<UserLoggedInHandler>();
            services.AddScoped<UserDeletedHandler>();
        });
    }

    public async ValueTask InitializeAsync()
    {
        await _db.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public new async ValueTask DisposeAsync()
    {
        await _db.StopAsync();
        await base.DisposeAsync();
    }
}
