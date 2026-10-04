using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Samples.Wholesale.PersistenceDemo;
using ModulithFoundry.Samples.Wholesale.PersistenceDemo.Inventory;
using ModulithFoundry.Tenancy;

string connection =
    Environment.GetEnvironmentVariable("WHOLESALE_DEMO_CONNECTION_STRING")
    ?? throw new InvalidOperationException(
        "Set WHOLESALE_DEMO_CONNECTION_STRING to a disposable demo database."
    );
await using var provider = DemoComposition
    .CreateServices(connection)
    .BuildServiceProvider(
        new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }
    );

// Explicit first-increment fixture setup; module migrations belong to the following increment.
await using (var setup = provider.CreateAsyncScope())
{
    await setup
        .ServiceProvider.GetRequiredService<InventoryDbContext>()
        .Database.EnsureCreatedAsync();
}

foreach (var (organization, quantity) in new[] { ("wholesale-alpha", 42), ("wholesale-beta", 7) })
{
    await using var scope = provider.CreateAsyncScope();
    scope
        .ServiceProvider.GetRequiredService<ITenantContextInitializer>()
        .Initialize(TenantContext.ForTenant(new TenantId(organization)));
    var inventory = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    StockReference? reference = await inventory.Stock.SingleOrDefaultAsync(row =>
        row.Sku == "WIDGET"
    );
    if (reference is null)
    {
        reference = new StockReference
        {
            Id = Guid.NewGuid(),
            OrganizationKey = organization,
            Sku = "WIDGET",
            Quantity = quantity,
        };
        inventory.Stock.Add(reference);
        await inventory.SaveChangesAsync();
    }
    Console.WriteLine($"{organization}: WIDGET availability={reference.Quantity}");
}
