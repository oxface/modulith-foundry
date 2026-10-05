using ModulithFoundry.Samples.Wholesale.HttpIdentityDemo;
using ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Access;

var builder = WebApplication.CreateBuilder(args);
DemoComposition.AddServices(builder.Services, builder.Configuration);
builder.Services.AddOrganizationTenancyFromRoute("organization");
var app = builder.Build();
DemoComposition.ConfigureHttp(app);
DemoComposition.MapOrganizationEndpoints(
    app,
    "/organizations/{organization}/catalog",
    "/organizations/{organization}/identity"
);
await app.RunAsync();
