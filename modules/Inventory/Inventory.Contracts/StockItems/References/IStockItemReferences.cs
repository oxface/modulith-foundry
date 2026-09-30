using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public interface IStockItemReferences
{
    Task<StockItemReferenceResolution> ResolveAsync(
        OrganizationId organizationId,
        IReadOnlyCollection<StockItemId> stockItemIds,
        CancellationToken cancellationToken = default);
}
