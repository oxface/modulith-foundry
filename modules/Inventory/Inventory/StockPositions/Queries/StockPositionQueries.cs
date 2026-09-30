using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;
using ModulithFoundry.Modules.Inventory.ReferenceData;

namespace ModulithFoundry.Modules.Inventory.StockPositions.Queries;

internal sealed class StockPositionQueries(
    InventoryDbContext context,
    InventoryRequestAuthorization requestAuthorization)
{
    internal async Task<GetStockPositionResult> GetCurrentAsync(
        GetStockPositionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!await requestAuthorization.HasPermissionAsync(
                query.ActorUserId,
                query.OrganizationId,
                InventoryPermissionIds.StockView,
                cancellationToken))
        {
            return new GetStockPositionResult.PermissionDenied();
        }

        string sku;
        string locationCode;
        try
        {
            sku = InventoryCode.Normalize(query.Sku, "sku", 64);
            locationCode = InventoryCode.Normalize(
                query.StockingLocationCode,
                "stockingLocationCode",
                64);
        }
        catch (InvalidInventoryReferenceDataException exception)
        {
            return new GetStockPositionResult.Invalid(exception.Field, exception.Message);
        }

        StockPositionView? position = await (
            from current in context.StockPositionWriteModels.AsNoTracking()
            join item in context.StockItems.AsNoTracking()
                on current.StockItemId equals item.Id
            join location in context.StockingLocations.AsNoTracking()
                on current.StockingLocationId equals location.Id
            where item.Sku == sku && location.Code == locationCode
            select new StockPositionView(
                new StockPositionId(current.StreamId),
                new StockItemId(current.StockItemId),
                new StockingLocationId(current.StockingLocationId),
                item.Sku,
                location.Code,
                current.BaseUnitCode,
                current.OnHandQuantity,
                current.ReservedQuantity,
                current.AvailableQuantity,
                current.Version))
            .SingleOrDefaultAsync(cancellationToken);

        return position is null
            ? new GetStockPositionResult.NotFound()
            : new GetStockPositionResult.Found(position);
    }
}
