using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockItems;

internal sealed class StockItemReferences(
    InventoryDbContext context,
    IOrganizationContextAccessor organizationContextAccessor
) : IStockItemReferences
{
    public async Task<StockItemReferenceResolution> ResolveAsync(
        OrganizationId organizationId,
        IReadOnlyCollection<StockItemId> stockItemIds,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(stockItemIds);
        if (organizationContextAccessor.OrganizationContext?.OrganizationId != organizationId)
        {
            throw new InvalidOperationException(
                "The requested organization does not match the resolved context."
            );
        }

        StockItemId[] requested = [.. stockItemIds.Distinct()];
        Guid[] ids = [.. requested.Select(itemId => itemId.Value)];
        var found = await context
            .StockItems.AsNoTracking()
            .Where(item => ids.Contains(item.Id))
            .Select(item => new
            {
                item.Id,
                item.Sku,
                item.Description,
                item.BaseUnitCode,
                item.IsActive,
            })
            .ToArrayAsync(cancellationToken);
        var foundById = found.ToDictionary(item => item.Id);

        return new StockItemReferenceResolution(
            [
                .. requested
                    .Where(itemId => foundById.GetValueOrDefault(itemId.Value)?.IsActive is true)
                    .Select(itemId => foundById[itemId.Value])
                    .Select(item => new StockItemReference(
                        new StockItemId(item.Id),
                        item.Sku,
                        item.Description,
                        item.BaseUnitCode
                    )),
            ],
            [.. requested.Where(itemId => !foundById.ContainsKey(itemId.Value))],
            [
                .. requested.Where(itemId =>
                    foundById.GetValueOrDefault(itemId.Value)?.IsActive is false
                ),
            ]
        );
    }
}
