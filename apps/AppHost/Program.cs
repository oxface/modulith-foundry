IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

IResourceBuilder<ParameterResource> postgresPassword = builder.AddParameter(
    "postgres-password",
    secret: true);
IResourceBuilder<ParameterResource> keycloakPassword = builder.AddParameter(
    "keycloak-password",
    secret: true);
IResourceBuilder<ParameterResource> oidcClientSecret = builder.AddParameter(
    "oidc-client-secret",
    secret: true);
IResourceBuilder<ParameterResource> keycloakTestUserPassword = builder.AddParameter(
    "keycloak-test-user-password",
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

IResourceBuilder<RedisResource> redis = builder
    .AddRedis("redis", port: 56379)
    .WithImageTag("8.2.10")
    .WithDataVolume();

IResourceBuilder<KeycloakResource> keycloak = builder
    .AddKeycloak("keycloak", 58080, adminPassword: keycloakPassword)
    .WithImageTag("26.7.4")
    .WithDataVolume()
    .WithRealmImport("./Keycloak/Realms")
    .WithEnvironment("MODULITH_FOUNDRY_CLIENT_SECRET", oidcClientSecret)
    .WithEnvironment("MODULITH_FOUNDRY_TEST_USER_PASSWORD", keycloakTestUserPassword);

IResourceBuilder<ProjectResource> migrator = builder
    .AddProject<Projects.ModulithFoundry_Migrator>("migrator")
    .WithReference(database)
    .WaitFor(database);

builder.AddProject<Projects.ModulithFoundry_Api>("api")
    .WithReference(database)
    .WithReference(redis)
    .WithReference(keycloak)
    .WithEnvironment(
        "Authentication__Oidc__Authority",
        ReferenceExpression.Create($"{keycloak.GetEndpoint("http")}/realms/modulith-foundry"))
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development")
    .WithEnvironment("Authentication__Oidc__ClientId", "modulith-foundry-bff")
    .WithEnvironment("Authentication__Oidc__ClientSecret", oidcClientSecret)
    .WithEnvironment("Authentication__Oidc__RequireHttpsMetadata", "true")
    .WithHttpEndpoint(port: 5080, name: "http")
    .WithHttpsEndpoint(port: 5443, name: "https")
    .WaitForCompletion(migrator)
    .WaitFor(redis)
    .WaitFor(keycloak)
    .WithHttpHealthCheck("/health");

builder.Build().Run();
