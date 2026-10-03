using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Purchasing.Persistence;

namespace ModulithFoundry.Modules.Purchasing.StockItemProjection;

internal sealed class StockItemProjectionQueries(PurchasingDbContext context)
    : IStockItemProjectionQueries
{
    public Task<StockItemProjectionStatus> GetStatusAsync(
        CancellationToken cancellationToken = default
    ) =>
        context
            .StockItemBootstrapCheckpoints.AsNoTracking()
            .Select(x => new StockItemProjectionStatus(x.IsReady, x.SnapshotWatermark))
            .SingleAsync(cancellationToken);

    public Task<StockItemProjectionView?> GetAsync(
        OrganizationId organizationId,
        Guid stockItemId,
        CancellationToken cancellationToken = default
    ) =>
        context
            .StockItemReferences.IgnoreQueryFilters([PurchasingDbContext.OrganizationScopeFilter])
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == organizationId.Value
                && x.StockItemId == stockItemId
                && context.StockItemBootstrapCheckpoints.Any(checkpoint => checkpoint.IsReady)
            )
            .Select(x => new StockItemProjectionView(
                x.OrganizationId,
                x.StockItemId,
                x.Sku,
                x.Description,
                x.BaseUnitCode,
                x.IsActive,
                x.SourceRevision
            ))
            .SingleOrDefaultAsync(cancellationToken);
}
