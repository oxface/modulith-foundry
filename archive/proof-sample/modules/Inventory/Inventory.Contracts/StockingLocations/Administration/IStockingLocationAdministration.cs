using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public interface IStockingLocationAdministration
{
    Task<CreateStockingLocationResult> CreateAsync(
        CreateStockingLocationCommand command,
        CancellationToken cancellationToken = default
    );

    Task<RenameStockingLocationResult> RenameAsync(
        RenameStockingLocationCommand command,
        CancellationToken cancellationToken = default
    );

    Task<SetStockingLocationActiveResult> SetActiveAsync(
        SetStockingLocationActiveCommand command,
        CancellationToken cancellationToken = default
    );

    Task<ListStockingLocationsResult> ListAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        CancellationToken cancellationToken = default
    );
}
