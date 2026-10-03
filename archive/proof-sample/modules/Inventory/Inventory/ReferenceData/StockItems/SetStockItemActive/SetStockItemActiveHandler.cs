using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems.Export;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockItems.SetStockItemActive;

internal sealed class SetStockItemActiveHandler(
    InventoryDbContext context,
    InventoryRequestAuthorization requestAuthorization,
    StockItemReferencePublisher publisher,
    TimeProvider timeProvider
)
{
    internal async Task<SetStockItemActiveResult> HandleAsync(
        SetStockItemActiveCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!requestAuthorization.MatchesContext(command.ActorUserId, command.OrganizationId))
        {
            return new SetStockItemActiveResult.PermissionDenied();
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
                    InventoryAuditActions.StockItemStatusChangeDenied,
                    InventoryAuditSubjectTypes.StockItem,
                    timeProvider.GetUtcNow()
                )
            );
            await context.SaveChangesAsync(cancellationToken);
            return new SetStockItemActiveResult.PermissionDenied();
        }

        string sku;
        try
        {
            sku = InventoryCode.Normalize(command.Sku, "sku", 64);
        }
        catch (InvalidInventoryReferenceDataException exception)
        {
            return new SetStockItemActiveResult.Invalid(exception.Field, exception.Message);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        await publisher.LockAsync(cancellationToken);
        StockItem? item = await context.StockItems.SingleOrDefaultAsync(
            candidate => candidate.Sku == sku,
            cancellationToken
        );
        if (item is null)
        {
            return new SetStockItemActiveResult.NotFound();
        }

        await context.Entry(item).ReloadAsync(cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!item.SetActive(command.IsActive, now))
        {
            return new SetStockItemActiveResult.Unchanged(item.ToView());
        }

        publisher.Stage(item, now);
        context.AuditEntries.Add(
            InventoryAuditEntry.Succeeded(
                item.OrganizationId,
                command.ActorUserId.Value,
                InventoryAuditActions.StockItemStatusChanged,
                InventoryAuditSubjectTypes.StockItem,
                item.Id,
                new { item.Sku, item.IsActive },
                now
            )
        );
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SetStockItemActiveResult.Changed(item.ToView());
    }
}
