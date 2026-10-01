using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockItems.Queries;

internal sealed class StockItemQueries(
    InventoryDbContext context,
    InventoryRequestAuthorization requestAuthorization
)
{
    internal async Task<ListStockItemsResult> ListAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        CancellationToken cancellationToken
    )
    {
        if (
            !await requestAuthorization.HasPermissionAsync(
                actorUserId,
                organizationId,
                InventoryPermissionIds.ItemsManage,
                cancellationToken
            )
        )
        {
            return new ListStockItemsResult.PermissionDenied();
        }

        return new ListStockItemsResult.Listed(
            await context
                .StockItems.AsNoTracking()
                .OrderBy(item => item.Sku)
                .Select(item => item.ToView())
                .ToArrayAsync(cancellationToken)
        );
    }
}
