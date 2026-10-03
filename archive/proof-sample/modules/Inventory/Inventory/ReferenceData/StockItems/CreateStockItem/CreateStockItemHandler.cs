using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems.Export;
using Npgsql;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockItems.CreateStockItem;

internal sealed class CreateStockItemHandler(
    InventoryDbContext context,
    InventoryRequestAuthorization requestAuthorization,
    StockItemReferencePublisher publisher,
    TimeProvider timeProvider
)
{
    internal async Task<CreateStockItemResult> HandleAsync(
        CreateStockItemCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!requestAuthorization.MatchesContext(command.ActorUserId, command.OrganizationId))
        {
            return new CreateStockItemResult.PermissionDenied();
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
                    InventoryAuditActions.StockItemCreateDenied,
                    InventoryAuditSubjectTypes.StockItem,
                    timeProvider.GetUtcNow()
                )
            );
            await context.SaveChangesAsync(cancellationToken);
            return new CreateStockItemResult.PermissionDenied();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        StockItem item;
        try
        {
            item = StockItem.Create(
                Guid.CreateVersion7(now),
                command.OrganizationId.Value,
                command.Sku,
                command.Description,
                command.BaseUnitCode,
                now
            );
        }
        catch (InvalidInventoryReferenceDataException exception)
        {
            return new CreateStockItemResult.Invalid(exception.Field, exception.Message);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        await publisher.LockAsync(cancellationToken);
        context.StockItems.Add(item);
        publisher.Stage(item, now);
        context.AuditEntries.Add(
            InventoryAuditEntry.Succeeded(
                item.OrganizationId,
                command.ActorUserId.Value,
                InventoryAuditActions.StockItemCreated,
                InventoryAuditSubjectTypes.StockItem,
                item.Id,
                new
                {
                    item.Sku,
                    item.Description,
                    item.BaseUnitCode,
                },
                now
            )
        );

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateSku(exception))
        {
            return new CreateStockItemResult.SkuUnavailable(item.Sku);
        }

        return new CreateStockItemResult.Created(item.ToView());
    }

    private static bool IsDuplicateSku(DbUpdateException exception) =>
        exception.InnerException
            is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "ux_stock_items_organization_sku",
            };
}
