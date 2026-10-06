using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);
var password = builder.AddParameter("postgres-password", secret: true);
var postgres = builder.AddPostgres("postgres", password: password).WithImageTag("18.6");
if (builder.Configuration.GetValue("LocalDevelopment:UseDataVolume", true))
    postgres.WithDataVolume();
var database = postgres.AddDatabase("wholesale", "wholesale");

var api = builder
    .AddProject("api", "../HttpIdentityDemo/HttpIdentityDemo.csproj", launchProfileName: null)
    .WithReference(database, connectionName: "Access")
    .WithHttpEndpoint(name: "http")
    .WithHttpsEndpoint(name: "https")
    .WithHttpHealthCheck("/health/ready")
    .WaitFor(database);

// Optional finite command, not a startup dependency or seed reconciliation worker.
var setup = builder
    .AddProject(
        "demo-setup",
        "../HttpIdentityDemo/HttpIdentityDemo.csproj",
        launchProfileName: null
    )
    .WithReference(database, connectionName: "Access")
    .WithArgs("--initialize-demo")
    .WaitFor(database)
    .WithExplicitStart();

if (builder.Configuration.GetValue("LocalDevelopment:UseLocalIdentityProvider", false))
{
    var adminPassword = builder.AddParameter("keycloak-password", secret: true);
    var clientSecret = builder.AddParameter("oidc-client-secret", secret: true);
    var userPassword = builder.AddParameter("demo-user-password", secret: true);
    var keycloak = builder
        .AddKeycloak(
            "keycloak",
            builder.Configuration.GetValue("LocalDevelopment:KeycloakPort", 58081),
            adminPassword: adminPassword
        )
        .WithImageTag("26.8.0")
        .WithRealmImport("./Keycloak")
        .WithEnvironment("WHOLESALE_CLIENT_SECRET", clientSecret)
        .WithEnvironment("WHOLESALE_USER_PASSWORD", userPassword)
        .WithEnvironment(
            "WHOLESALE_CALLBACK",
            ReferenceExpression.Create(
                $"{api.GetEndpoint("https", KnownNetworkIdentifiers.LocalhostNetwork)}/signin-oidc"
            )
        );
    var issuer = ReferenceExpression.Create($"{keycloak.GetEndpoint("http")}/realms/wholesale");
    api.WithReference(keycloak)
        .WithEnvironment("Oidc__Authority", issuer)
        .WithEnvironment("Oidc__ClientId", "wholesale-bff")
        .WithEnvironment("Oidc__ClientSecret", clientSecret)
        .WaitFor(keycloak);
    setup
        .WithEnvironment("DemoIdentity__Issuer", issuer)
        .WithEnvironment("DemoIdentity__AlphaSubject", "20000000-0000-0000-0000-000000000001")
        .WithEnvironment("DemoIdentity__BetaSubject", "20000000-0000-0000-0000-000000000002")
        .WaitFor(keycloak);
}
else
{
    api.WithEnvironment("Oidc__Authority", builder.AddParameter("oidc-authority"))
        .WithEnvironment("Oidc__ClientId", builder.AddParameter("oidc-client-id"));
}

await builder.Build().RunAsync();
