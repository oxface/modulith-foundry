using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);
var password = builder.AddParameter("postgres-password", secret: true);
var authority = builder.AddParameter("oidc-authority");
var clientId = builder.AddParameter("oidc-client-id");
var postgres = builder.AddPostgres("postgres", password: password).WithImageTag("18.6");
if (builder.Configuration.GetValue("LocalDevelopment:UseDataVolume", true))
    postgres.WithDataVolume();
var database = postgres.AddDatabase("wholesale", "wholesale");

builder
    .AddProject("api", "../HttpIdentityDemo/HttpIdentityDemo.csproj", launchProfileName: null)
    .WithReference(database, connectionName: "Access")
    .WithEnvironment("Oidc__Authority", authority)
    .WithEnvironment("Oidc__ClientId", clientId)
    .WithHttpEndpoint(name: "http")
    .WithHttpsEndpoint(name: "https")
    .WithHttpHealthCheck("/health/ready")
    .WaitFor(database);

// Optional finite command, not a startup dependency or seed reconciliation worker.
builder
    .AddProject(
        "demo-setup",
        "../HttpIdentityDemo/HttpIdentityDemo.csproj",
        launchProfileName: null
    )
    .WithReference(database, connectionName: "Access")
    .WithArgs("--initialize-demo")
    .WaitFor(database)
    .WithExplicitStart();

await builder.Build().RunAsync();
