using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations.CreateStockingLocation;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations.Queries;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations.RenameStockingLocation;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations.SetStockingLocationActive;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations;

internal sealed class StockingLocationAdministration(
    CreateStockingLocationHandler create,
    RenameStockingLocationHandler rename,
    SetStockingLocationActiveHandler setActive,
    StockingLocationQueries queries
) : IStockingLocationAdministration
{
    public Task<CreateStockingLocationResult> CreateAsync(
        CreateStockingLocationCommand command,
        CancellationToken cancellationToken = default
    ) => create.HandleAsync(command, cancellationToken);

    public Task<RenameStockingLocationResult> RenameAsync(
        RenameStockingLocationCommand command,
        CancellationToken cancellationToken = default
    ) => rename.HandleAsync(command, cancellationToken);

    public Task<SetStockingLocationActiveResult> SetActiveAsync(
        SetStockingLocationActiveCommand command,
        CancellationToken cancellationToken = default
    ) => setActive.HandleAsync(command, cancellationToken);

    public Task<ListStockingLocationsResult> ListAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        CancellationToken cancellationToken = default
    ) => queries.ListAsync(actorUserId, organizationId, cancellationToken);
}
