using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public interface IStockItemReferenceResolver
{
    Task<StockItemReferenceResolution> ResolveAsync(
        OrganizationId organizationId,
        IReadOnlyCollection<StockItemId> stockItemIds,
        CancellationToken cancellationToken = default
    );
}
