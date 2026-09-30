using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;
using Npgsql;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations.CreateStockingLocation;

internal sealed class CreateStockingLocationHandler(
    InventoryDbContext context,
    InventoryRequestAuthorization requestAuthorization,
    TimeProvider timeProvider)
{
    internal async Task<CreateStockingLocationResult> HandleAsync(
        CreateStockingLocationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!requestAuthorization.MatchesContext(command.ActorUserId, command.OrganizationId))
        {
            return new CreateStockingLocationResult.PermissionDenied();
        }

        if (!await requestAuthorization.HasPermissionAsync(
                command.ActorUserId,
                command.OrganizationId,
                InventoryPermissionIds.LocationsManage,
                cancellationToken))
        {
            context.AuditEntries.Add(InventoryAuditEntry.PermissionDenied(
                command.OrganizationId.Value,
                command.ActorUserId.Value,
                InventoryAuditActions.StockingLocationCreateDenied,
                InventoryAuditSubjectTypes.StockingLocation,
                timeProvider.GetUtcNow()));
            await context.SaveChangesAsync(cancellationToken);
            return new CreateStockingLocationResult.PermissionDenied();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        StockingLocation location;
        try
        {
            location = StockingLocation.Create(
                Guid.CreateVersion7(now),
                command.OrganizationId.Value,
                command.Code,
                command.Name,
                now);
        }
        catch (InvalidInventoryReferenceDataException exception)
        {
            return new CreateStockingLocationResult.Invalid(exception.Field, exception.Message);
        }

        context.StockingLocations.Add(location);
        context.AuditEntries.Add(InventoryAuditEntry.Succeeded(
            location.OrganizationId,
            command.ActorUserId.Value,
            InventoryAuditActions.StockingLocationCreated,
            InventoryAuditSubjectTypes.StockingLocation,
            location.Id,
            new { location.Code, location.Name },
            now));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateCode(exception))
        {
            return new CreateStockingLocationResult.CodeUnavailable(location.Code);
        }

        return new CreateStockingLocationResult.Created(location.ToView());
    }

    private static bool IsDuplicateCode(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_stocking_locations_organization_code",
        };
}
