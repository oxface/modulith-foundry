using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;
using ModulithFoundry.Modules.Inventory.ReferenceData;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems;
using ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

namespace ModulithFoundry.Modules.Inventory.StockPositions.CorrectStockQuantity;

internal sealed class CorrectStockQuantityHandler(
    InventoryDbContext context,
    StockPositionStore store,
    InventoryRequestAuthorization authorization,
    TimeProvider timeProvider)
{
    internal async Task<CorrectStockQuantityResult> HandleAsync(
        CorrectStockQuantityCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!authorization.MatchesContext(command.ActorUserId, command.OrganizationId))
        {
            return new CorrectStockQuantityResult.PermissionDenied();
        }

        if (!await authorization.HasPermissionAsync(
                command.ActorUserId, command.OrganizationId, InventoryPermissionIds.StockAdjust, cancellationToken))
        {
            context.AuditEntries.Add(InventoryAuditEntry.PermissionDenied(
                command.OrganizationId.Value, command.ActorUserId.Value,
                InventoryAuditActions.StockQuantityCorrectionDenied, InventoryAuditSubjectTypes.StockPosition,
                timeProvider.GetUtcNow()));
            await context.SaveChangesAsync(cancellationToken);
            return new CorrectStockQuantityResult.PermissionDenied();
        }

        StockPositionAggregate aggregate;
        StockItem? item;
        StockingLocation? location;
        try
        {
            string sku = InventoryCode.Normalize(command.Sku, "sku", 64);
            string code = InventoryCode.Normalize(command.StockingLocationCode, "stockingLocationCode", 64);
            _ = Quantity.NonNegative(command.OnHandQuantity);
            _ = StockCorrectionReason.Create(command.Reason);
            item = await context.StockItems.SingleOrDefaultAsync(candidate => candidate.Sku == sku, cancellationToken);
            location = await context.StockingLocations.SingleOrDefaultAsync(candidate => candidate.Code == code, cancellationToken);
            if (item is null || location is null) { return new CorrectStockQuantityResult.NotFound(); }

            aggregate = await store.LoadForWritingAsync(item.Id, location.Id, command.ExpectedVersion, cancellationToken);
            if (aggregate.State is null) { return new CorrectStockQuantityResult.NotFound(); }
            aggregate.CorrectQuantity(command.OnHandQuantity, command.Reason);
            if (aggregate.UncommittedEvents.Count == 0)
            {
                return new CorrectStockQuantityResult.Unchanged(aggregate.ToView(item, location));
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            await store.StageAppendAsync(command.OrganizationId, command.ActorUserId, aggregate, now, cancellationToken);
            context.AuditEntries.Add(InventoryAuditEntry.Succeeded(
                command.OrganizationId.Value, command.ActorUserId.Value,
                InventoryAuditActions.StockQuantityCorrected, InventoryAuditSubjectTypes.StockPosition,
                aggregate.StreamId,
                new { item.Sku, StockingLocationCode = location.Code, command.OnHandQuantity, Version = aggregate.Version },
                now));
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidInventoryReferenceDataException exception)
        {
            return new CorrectStockQuantityResult.Invalid(exception.Field, exception.Message);
        }
        catch (InvalidStockPositionValueException exception)
        {
            return new CorrectStockQuantityResult.Invalid(
                exception.Field == "quantity" ? "onHandQuantity" : exception.Field, exception.Message);
        }
        catch (StockPositionConcurrencyException)
        {
            return new CorrectStockQuantityResult.VersionConflict();
        }
        catch (DbUpdateException exception) when (StockPositionStore.IsConcurrencyConflict(exception))
        {
            return new CorrectStockQuantityResult.VersionConflict();
        }

        return new CorrectStockQuantityResult.Corrected(aggregate.ToView(item, location));
    }
}
