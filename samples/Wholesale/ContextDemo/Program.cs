using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.ExecutionIdentity;
using ModulithFoundry.Samples.Wholesale.ContextDemo;
using ModulithFoundry.Samples.Wholesale.ContextDemo.Inventory.Contracts;
using ModulithFoundry.Samples.Wholesale.ContextDemo.Sales.Contracts;

await using var provider = DemoComposition
    .CreateServices()
    .BuildServiceProvider(
        new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }
    );

// These are explicit demonstration identities, with no authentication or HTTP ingress.
var human = Actor.Human(new ActorId("demo-user-alex"));
var workflow = Actor.System(new ActorId("sales.order-fulfilment"));
string[] availability = await Task.WhenAll(
    Task.Run(() => ReadAvailability(provider, "wholesale-alpha")),
    Task.Run(() => ReadAvailability(provider, "wholesale-beta"))
);
foreach (string line in availability)
{
    Console.WriteLine(line);
}

Console.WriteLine(Preview(provider, "wholesale-alpha", human));
Console.WriteLine(Preview(provider, "wholesale-beta", workflow, human));

using (var scope = provider.CreateScope())
{
    scope
        .ServiceProvider.GetRequiredService<IOperationContextInitializer>()
        .Initialize(OperationContext.Tenantless(Actor.Anonymous));
    OperationContext context = scope
        .ServiceProvider.GetRequiredService<IOperationContextAccessor>()
        .Current;
    Console.WriteLine($"host-info: tenantless, actor={context.Actor.Kind}");
}

using (var scope = provider.CreateScope())
{
    scope
        .ServiceProvider.GetRequiredService<IOperationContextInitializer>()
        .Initialize(OperationContext.Tenantless(Actor.System(new ActorId("sample.maintenance"))));
    OperationContext context = scope
        .ServiceProvider.GetRequiredService<IOperationContextAccessor>()
        .Current;
    Actor actor = context.RequireIdentifiedActor();
    Console.WriteLine($"maintenance: tenantless, actor={actor.Id!.Value}");
}

static string ReadAvailability(ServiceProvider provider, string tenantKey)
{
    using var scope = provider.CreateScope();
    scope
        .ServiceProvider.GetRequiredService<IOperationContextInitializer>()
        .Initialize(OperationContext.ForTenant(new TenantId(tenantKey), Actor.Anonymous));
    int available = scope
        .ServiceProvider.GetRequiredService<IStockAvailability>()
        .GetAvailableQuantity("WIDGET");
    return $"{tenantKey}: anonymous availability={available}";
}

static string Preview(
    ServiceProvider provider,
    string tenantKey,
    Actor actor,
    Actor? initiator = null
)
{
    using var scope = provider.CreateScope();
    scope
        .ServiceProvider.GetRequiredService<IOperationContextInitializer>()
        .Initialize(OperationContext.ForTenant(new TenantId(tenantKey), actor, initiator));
    DraftPreview preview = scope
        .ServiceProvider.GetRequiredService<IDraftOrderPreview>()
        .Preview("WIDGET", 10);
    return $"{tenantKey}: requested={preview.RequestedQuantity}, available={preview.AvailableQuantity}, "
        + $"can-fulfil={preview.CanFulfil}, actor={preview.RequestedBy.Kind}:{preview.RequestedBy.Id!.Value}, "
        + $"initiator={preview.Initiator?.Id?.Value ?? "none"}";
}
