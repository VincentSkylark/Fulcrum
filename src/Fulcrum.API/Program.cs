using Fulcrum.API.Data;
using Fulcrum.API.Endpoints;
using Fulcrum.API.Handlers;
using Fulcrum.Auth;
using Fulcrum.Core.Events;
using Fulcrum.News;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Wolverine;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Fulcrum host");

    var builder = WebApplication.CreateBuilder(args);

    builder.AddServiceDefaults();

    builder.Host.UseWolverine();

    builder.Services.AddFulcrumAuth(builder.Configuration);
    builder.Services.AddFulcrumNews(builder.Configuration);

    builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    {
        var connectionString = builder.Configuration.GetConnectionString("app-db");
        if (!string.IsNullOrEmpty(connectionString))
            options.UseNpgsql(connectionString);
    });

    builder.Services.AddScoped<IEventBus, WolverineEventBus>();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    app.UseFulcrumAuthMiddleware();

    app.MapDefaultEndpoints();
    app.MapFulcrumAuthEndpoints();
    ProfileEndpoints.Map(app);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Fulcrum host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
