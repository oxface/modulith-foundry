using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Samples.Wholesale.HttpIdentityDemo;
using ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Access;
using ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Access.Persistence;

bool initializeAccess = args is ["--initialize-access"];
var builder = WebApplication.CreateBuilder(initializeAccess ? [] : args);
if (initializeAccess)
{
    var options = new DbContextOptionsBuilder<AccessDbContext>();
    AccessDatabase.Configure(options, AccessDatabase.ConnectionString(builder.Configuration));
    await using var database = new AccessDbContext(options.Options);
    await database.Database.MigrateAsync();
    await using var transaction = await database.Database.BeginTransactionAsync();
    AccessDemoSeed.Stage(database);
    await database.SaveChangesAsync();
    await transaction.CommitAsync();
    Console.WriteLine("Access demo initialized; HTTP host was not started.");
    return;
}
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
