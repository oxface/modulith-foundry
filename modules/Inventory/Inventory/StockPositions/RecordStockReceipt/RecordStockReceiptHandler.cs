using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;
using ModulithFoundry.Modules.Inventory.ReferenceData;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems;
using ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

namespace ModulithFoundry.Modules.Inventory.StockPositions.RecordStockReceipt;

internal sealed class RecordStockReceiptHandler(
    InventoryDbContext context,
    StockPositionStore store,
    InventoryRequestAuthorization requestAuthorization,
    TimeProvider timeProvider)
{
    internal async Task<RecordStockReceiptResult> HandleAsync(
        RecordStockReceiptCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!requestAuthorization.MatchesContext(command.ActorUserId, command.OrganizationId))
        {
            return new RecordStockReceiptResult.PermissionDenied();
        }

        if (!await requestAuthorization.HasPermissionAsync(
                command.ActorUserId,
                command.OrganizationId,
                InventoryPermissionIds.StockAdjust,
                cancellationToken))
        {
            context.AuditEntries.Add(InventoryAuditEntry.PermissionDenied(
                command.OrganizationId.Value,
                command.ActorUserId.Value,
                InventoryAuditActions.StockReceiptRecordDenied,
                InventoryAuditSubjectTypes.StockPosition,
                timeProvider.GetUtcNow()));
            await context.SaveChangesAsync(cancellationToken);
            return new RecordStockReceiptResult.PermissionDenied();
        }

        string sku;
        string locationCode;
        try
        {
            sku = InventoryCode.Normalize(command.Sku, "sku", 64);
            locationCode = InventoryCode.Normalize(
                command.StockingLocationCode,
                "stockingLocationCode",
                64);
            Quantity.Positive(command.Quantity);
        }
        catch (ArgumentException exception) when (exception is InvalidInventoryReferenceDataException
            or InvalidStockPositionValueException)
        {
            string field = exception switch
            {
                InvalidInventoryReferenceDataException invalid => invalid.Field,
                InvalidStockPositionValueException invalid => invalid.Field,
                _ => throw new UnreachableException(),
            };
            return new RecordStockReceiptResult.Invalid(field, exception.Message);
        }

        StockItem? item = await context.StockItems.SingleOrDefaultAsync(
            candidate => candidate.Sku == sku,
            cancellationToken);
        if (item is null || !item.IsActive)
        {
            return new RecordStockReceiptResult.ReferenceUnavailable("stock-item");
        }

        StockingLocation? location = await context.StockingLocations.SingleOrDefaultAsync(
            candidate => candidate.Code == locationCode,
            cancellationToken);
        if (location is null || !location.IsActive)
        {
            return new RecordStockReceiptResult.ReferenceUnavailable("stocking-location");
        }

        StockPositionAggregate aggregate;
        try
        {
            aggregate = await store.LoadForWritingAsync(
                item.Id, location.Id, command.ExpectedVersion, cancellationToken);
            aggregate.RecordReceipt(item.Id, location.Id, item.BaseUnitCode, command.Quantity);

            DateTimeOffset now = timeProvider.GetUtcNow();
            await store.StageAppendAsync(
                command.OrganizationId,
                command.ActorUserId,
                aggregate,
                now,
                cancellationToken);
            context.AuditEntries.Add(InventoryAuditEntry.Succeeded(
                command.OrganizationId.Value,
                command.ActorUserId.Value,
                InventoryAuditActions.StockReceiptRecorded,
                InventoryAuditSubjectTypes.StockPosition,
                aggregate.StreamId,
                new
                {
                    item.Sku,
                    StockingLocationCode = location.Code,
                    command.Quantity,
                    Version = aggregate.Version,
                },
                now));

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (StockPositionConcurrencyException)
        {
            return new RecordStockReceiptResult.VersionConflict();
        }
        catch (InvalidStockPositionValueException exception)
        {
            return new RecordStockReceiptResult.Invalid(exception.Field, exception.Message);
        }
        catch (DbUpdateException exception) when (StockPositionStore.IsConcurrencyConflict(exception))
        {
            return new RecordStockReceiptResult.VersionConflict();
        }

        return new RecordStockReceiptResult.Recorded(aggregate.ToView(item, location));
    }
}
