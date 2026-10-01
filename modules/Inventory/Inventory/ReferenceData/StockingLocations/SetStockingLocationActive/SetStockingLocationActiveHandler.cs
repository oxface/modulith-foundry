using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations.SetStockingLocationActive;

internal sealed class SetStockingLocationActiveHandler(
    InventoryDbContext context,
    InventoryRequestAuthorization requestAuthorization,
    TimeProvider timeProvider
)
{
    internal async Task<SetStockingLocationActiveResult> HandleAsync(
        SetStockingLocationActiveCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!requestAuthorization.MatchesContext(command.ActorUserId, command.OrganizationId))
        {
            return new SetStockingLocationActiveResult.PermissionDenied();
        }

        if (
            !await requestAuthorization.HasPermissionAsync(
                command.ActorUserId,
                command.OrganizationId,
                InventoryPermissionIds.LocationsManage,
                cancellationToken
            )
        )
        {
            context.AuditEntries.Add(
                InventoryAuditEntry.PermissionDenied(
                    command.OrganizationId.Value,
                    command.ActorUserId.Value,
                    InventoryAuditActions.StockingLocationStatusChangeDenied,
                    InventoryAuditSubjectTypes.StockingLocation,
                    timeProvider.GetUtcNow()
                )
            );
            await context.SaveChangesAsync(cancellationToken);
            return new SetStockingLocationActiveResult.PermissionDenied();
        }

        string code;
        try
        {
            code = InventoryCode.Normalize(command.Code, "code", 64);
        }
        catch (InvalidInventoryReferenceDataException exception)
        {
            return new SetStockingLocationActiveResult.Invalid(exception.Field, exception.Message);
        }

        StockingLocation? location = await context.StockingLocations.SingleOrDefaultAsync(
            candidate => candidate.Code == code,
            cancellationToken
        );
        if (location is null)
        {
            return new SetStockingLocationActiveResult.NotFound();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!location.SetActive(command.IsActive, now))
        {
            return new SetStockingLocationActiveResult.Unchanged(location.ToView());
        }

        context.AuditEntries.Add(
            InventoryAuditEntry.Succeeded(
                location.OrganizationId,
                command.ActorUserId.Value,
                InventoryAuditActions.StockingLocationStatusChanged,
                InventoryAuditSubjectTypes.StockingLocation,
                location.Id,
                new { location.Code, location.IsActive },
                now
            )
        );
        await context.SaveChangesAsync(cancellationToken);
        return new SetStockingLocationActiveResult.Changed(location.ToView());
    }
}
