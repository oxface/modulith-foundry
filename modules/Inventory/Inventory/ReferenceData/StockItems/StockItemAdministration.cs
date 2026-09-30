using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems.ChangeStockItemDescription;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems.CreateStockItem;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems.Queries;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems.SetStockItemActive;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockItems;

internal sealed class StockItemAdministration(
    CreateStockItemHandler create,
    ChangeStockItemDescriptionHandler changeDescription,
    SetStockItemActiveHandler setActive,
    StockItemQueries queries) : IStockItemAdministration
{
    public Task<CreateStockItemResult> CreateAsync(
        CreateStockItemCommand command,
        CancellationToken cancellationToken = default) =>
        create.HandleAsync(command, cancellationToken);

    public Task<ChangeStockItemDescriptionResult> ChangeDescriptionAsync(
        ChangeStockItemDescriptionCommand command,
        CancellationToken cancellationToken = default) =>
        changeDescription.HandleAsync(command, cancellationToken);

    public Task<SetStockItemActiveResult> SetActiveAsync(
        SetStockItemActiveCommand command,
        CancellationToken cancellationToken = default) =>
        setActive.HandleAsync(command, cancellationToken);

    public Task<ListStockItemsResult> ListAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        CancellationToken cancellationToken = default) =>
        queries.ListAsync(actorUserId, organizationId, cancellationToken);
}
