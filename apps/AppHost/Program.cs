IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ParameterResource> postgresPassword = builder.AddParameter(
    "postgres-password",
    secret: true);
IResourceBuilder<PostgresServerResource> postgres = builder
    .AddPostgres("postgres", password: postgresPassword, port: 55432)
    .WithImageTag("18.6")
    .WithDataVolume()
    .WithPgAdmin(pgAdmin => pgAdmin
        .WithHostPort(5050)
        .WithExplicitStart());
IResourceBuilder<PostgresDatabaseResource> database = postgres
    .AddDatabase("database", "modulith_foundry");

IResourceBuilder<ProjectResource> migrator = builder
    .AddProject<Projects.ModulithFoundry_Migrator>("migrator")
    .WithReference(database)
    .WaitFor(database);

builder.AddProject<Projects.ModulithFoundry_Api>("api")
    .WithReference(database)
    .WithHttpEndpoint(port: 5080, name: "http")
    .WaitForCompletion(migrator)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
