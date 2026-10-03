using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations.RenameStockingLocation;

internal sealed class RenameStockingLocationHandler(
    InventoryDbContext context,
    InventoryRequestAuthorization requestAuthorization,
    TimeProvider timeProvider
)
{
    internal async Task<RenameStockingLocationResult> HandleAsync(
        RenameStockingLocationCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!requestAuthorization.MatchesContext(command.ActorUserId, command.OrganizationId))
        {
            return new RenameStockingLocationResult.PermissionDenied();
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
                    InventoryAuditActions.StockingLocationRenameDenied,
                    InventoryAuditSubjectTypes.StockingLocation,
                    timeProvider.GetUtcNow()
                )
            );
            await context.SaveChangesAsync(cancellationToken);
            return new RenameStockingLocationResult.PermissionDenied();
        }

        string code;
        try
        {
            code = InventoryCode.Normalize(command.Code, "code", 64);
        }
        catch (InvalidInventoryReferenceDataException exception)
        {
            return new RenameStockingLocationResult.Invalid(exception.Field, exception.Message);
        }

        StockingLocation? location = await context.StockingLocations.SingleOrDefaultAsync(
            candidate => candidate.Code == code,
            cancellationToken
        );
        if (location is null)
        {
            return new RenameStockingLocationResult.NotFound();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        try
        {
            if (!location.Rename(command.Name, now))
            {
                return new RenameStockingLocationResult.Unchanged(location.ToView());
            }
        }
        catch (InvalidInventoryReferenceDataException exception)
        {
            return new RenameStockingLocationResult.Invalid(exception.Field, exception.Message);
        }

        context.AuditEntries.Add(
            InventoryAuditEntry.Succeeded(
                location.OrganizationId,
                command.ActorUserId.Value,
                InventoryAuditActions.StockingLocationRenamed,
                InventoryAuditSubjectTypes.StockingLocation,
                location.Id,
                new { location.Code, location.Name },
                now
            )
        );
        await context.SaveChangesAsync(cancellationToken);
        return new RenameStockingLocationResult.Renamed(location.ToView());
    }
}
