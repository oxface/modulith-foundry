using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations.Queries;

internal sealed class StockingLocationQueries(
    InventoryDbContext context,
    InventoryRequestAuthorization requestAuthorization)
{
    internal async Task<ListStockingLocationsResult> ListAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        CancellationToken cancellationToken)
    {
        if (!await requestAuthorization.HasPermissionAsync(
                actorUserId,
                organizationId,
                InventoryPermissionIds.LocationsManage,
                cancellationToken))
        {
            return new ListStockingLocationsResult.PermissionDenied();
        }

        return new ListStockingLocationsResult.Listed(await context.StockingLocations
            .AsNoTracking()
            .OrderBy(location => location.Code)
            .Select(location => location.ToView())
            .ToArrayAsync(cancellationToken));
    }
}
