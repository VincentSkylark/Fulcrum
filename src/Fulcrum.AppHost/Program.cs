var builder = DistributedApplication.CreateBuilder(args);

var pgPassword = builder.AddParameter("pg-password", builder.Configuration["Postgres:Password"] ?? "changeme");

var postgres = builder.AddPostgres("postgres", password: pgPassword);

if (builder.ExecutionContext.IsRunMode)
{
    postgres
        .WithDataBindMount("../../data/postgres")
        .WithPgAdmin();
}

var appDb = postgres.AddDatabase("app-db");
var kratosDb = postgres.AddDatabase("kratos-db");

var kratosDsn = builder.Configuration["Kratos:Dsn"]
    ?? $"postgres://postgres:changeme@postgres:5432/kratos-db?sslmode=disable";

if (builder.ExecutionContext.IsRunMode)
{
    var webhookSecret = builder.Configuration["Kratos:WebhookSecret"] ?? "dev-webhook-secret";

    builder.AddContainer("kratos", "oryd/kratos:v1.3.1")
        .WithHttpEndpoint(targetPort: 4433, port: 4433, name: "public")
        .WithHttpEndpoint(targetPort: 4434, port: 4434, name: "admin")
        .WithEnvironment("DSN", kratosDsn)
        .WithEnvironment("WEBHOOK_SECRET", webhookSecret)
        .WithBindMount("../../kratos/config", "/etc/config/kratos")
        .WithArgs("serve", "-c", "/etc/config/kratos/kratos.yml", "--dev")
        .WaitFor(kratosDb);
}

var api = builder.AddProject<Projects.Fulcrum_API>("api")
    .WithReference(appDb)
    .WithReference(kratosDb)
    .WithExternalHttpEndpoints();

builder.Build().Run();
