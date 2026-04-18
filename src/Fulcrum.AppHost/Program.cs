var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("app-db");

// TODO: Enable Kratos container during Phase 2 (Auth) once kratos/ config files exist
// var kratosDb = postgres.AddDatabase("kratos-db");
// var kratos = builder.AddContainer("kratos", "oryd/kratos:v1.3.1")
//     .WithHttpEndpoint(targetPort: 4433, port: 4433, name: "public")
//     .WithHttpEndpoint(targetPort: 4434, port: 4434, name: "admin")
//     .WithReference(kratosDb)
//     .WithEnvironment("DSN", "postgres://kratos:kratos@postgres:5432/kratos-db?sslmode=disable")
//     .WithBindMount("../kratos/config", "/home/kratos/config")
//     .WithArgs("serve", "-c", "/home/kratos/config/kratos.yml", "--dev");

var api = builder.AddProject<Projects.Fulcrum_API>("api")
    .WithReference(postgres)
    .WithExternalHttpEndpoints();

builder.Build().Run();
