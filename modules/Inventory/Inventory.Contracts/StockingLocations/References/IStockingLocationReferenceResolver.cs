using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

// Trusted in-process reference lookup, including workflow-created scopes. Not HTTP ingress.
public interface IStockingLocationReferenceResolver
{
    Task<StockingLocationId?> FindActiveAsync(
        OrganizationId organizationId,
        string code,
        CancellationToken cancellationToken = default
    );
}
