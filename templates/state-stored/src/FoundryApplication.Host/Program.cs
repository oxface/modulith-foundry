using ConsumerRoot.Catalog;
using ConsumerRoot.Host;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

if (args.Length == 0)
{
    Console.WriteLine(
        "FoundryApplication: storage is not initialized on startup. Use 'migrate' explicitly; run the adoption tests for the Catalog journey."
    );
    return 0;
}
if (args is not ["migrate"])
{
    Console.Error.WriteLine("Supported command: migrate");
    return 2;
}
string? connectionString = Environment.GetEnvironmentVariable("CATALOG_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("migrate requires CATALOG_CONNECTION_STRING.");
    return 2;
}
using var services = Composition
    .CreateServices(connectionString)
    .BuildServiceProvider(
        new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }
    );
await using var scope = services.CreateAsyncScope();
await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
Console.WriteLine("Catalog migration applied explicitly. No data seeded.");
return 0;
