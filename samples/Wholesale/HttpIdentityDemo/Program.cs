using ModulithFoundry.Samples.Wholesale.HttpIdentityDemo;
using ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.HttpIntegration;

bool initializeAccess = args is ["--initialize-access"];
bool initializeDemo = args is ["--initialize-demo"];
var builder = WebApplication.CreateBuilder(initializeAccess || initializeDemo ? [] : args);
if (initializeAccess || initializeDemo)
{
    string connection =
        builder.Configuration.GetConnectionString("Access")
        ?? throw new InvalidOperationException(
            "Configure ConnectionStrings:Access for the HTTP sample."
        );
    await DemoSetup.InitializeAsync(connection, initializeDemo, CancellationToken.None);
    Console.WriteLine(
        initializeDemo
            ? "Wholesale demo initialized; HTTP host was not started."
            : "Access demo initialized; HTTP host was not started."
    );
    return;
}
DemoComposition.AddServices(builder.Services, builder.Configuration);
builder.Services.AddOrganizationTenancyFromRoute("organization");
var app = builder.Build();
DemoComposition.ConfigureHttp(app);
DemoComposition.MapOrganizationEndpoints(
    app,
    "/organizations/{organization}/catalog",
    "/organizations/{organization}/identity",
    "/organizations/{organization}/stock/{sku}"
);
ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Endpoints.CustomerProfileEndpoints.Map(
    app,
    "/organizations/{organization}/customers/{customerId:guid}/profile"
);
await app.RunAsync();
