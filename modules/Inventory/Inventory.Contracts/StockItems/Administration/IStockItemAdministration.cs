using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public interface IStockItemAdministration
{
    Task<CreateStockItemResult> CreateAsync(
        CreateStockItemCommand command,
        CancellationToken cancellationToken = default
    );

    Task<ChangeStockItemDescriptionResult> ChangeDescriptionAsync(
        ChangeStockItemDescriptionCommand command,
        CancellationToken cancellationToken = default
    );

    Task<SetStockItemActiveResult> SetActiveAsync(
        SetStockItemActiveCommand command,
        CancellationToken cancellationToken = default
    );

    Task<ListStockItemsResult> ListAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        CancellationToken cancellationToken = default
    );
}
