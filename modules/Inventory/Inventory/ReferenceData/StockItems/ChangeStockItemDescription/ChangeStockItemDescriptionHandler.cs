using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockItems.ChangeStockItemDescription;

internal sealed class ChangeStockItemDescriptionHandler(
    InventoryDbContext context,
    InventoryRequestAuthorization requestAuthorization,
    TimeProvider timeProvider
)
{
    internal async Task<ChangeStockItemDescriptionResult> HandleAsync(
        ChangeStockItemDescriptionCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!requestAuthorization.MatchesContext(command.ActorUserId, command.OrganizationId))
        {
            return new ChangeStockItemDescriptionResult.PermissionDenied();
        }

        if (
            !await requestAuthorization.HasPermissionAsync(
                command.ActorUserId,
                command.OrganizationId,
                InventoryPermissionIds.ItemsManage,
                cancellationToken
            )
        )
        {
            context.AuditEntries.Add(
                InventoryAuditEntry.PermissionDenied(
                    command.OrganizationId.Value,
                    command.ActorUserId.Value,
                    InventoryAuditActions.StockItemDescriptionChangeDenied,
                    InventoryAuditSubjectTypes.StockItem,
                    timeProvider.GetUtcNow()
                )
            );
            await context.SaveChangesAsync(cancellationToken);
            return new ChangeStockItemDescriptionResult.PermissionDenied();
        }

        string sku;
        try
        {
            sku = InventoryCode.Normalize(command.Sku, "sku", 64);
        }
        catch (InvalidInventoryReferenceDataException exception)
        {
            return new ChangeStockItemDescriptionResult.Invalid(exception.Field, exception.Message);
        }

        StockItem? item = await context.StockItems.SingleOrDefaultAsync(
            candidate => candidate.Sku == sku,
            cancellationToken
        );
        if (item is null)
        {
            return new ChangeStockItemDescriptionResult.NotFound();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        try
        {
            if (!item.ChangeDescription(command.Description, now))
            {
                return new ChangeStockItemDescriptionResult.Unchanged(item.ToView());
            }
        }
        catch (InvalidInventoryReferenceDataException exception)
        {
            return new ChangeStockItemDescriptionResult.Invalid(exception.Field, exception.Message);
        }

        context.AuditEntries.Add(
            InventoryAuditEntry.Succeeded(
                item.OrganizationId,
                command.ActorUserId.Value,
                InventoryAuditActions.StockItemDescriptionChanged,
                InventoryAuditSubjectTypes.StockItem,
                item.Id,
                new { item.Sku, item.Description },
                now
            )
        );
        await context.SaveChangesAsync(cancellationToken);
        return new ChangeStockItemDescriptionResult.Changed(item.ToView());
    }
}
