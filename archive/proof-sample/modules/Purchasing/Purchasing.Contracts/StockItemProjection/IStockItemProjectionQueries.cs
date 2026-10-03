using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Purchasing.Contracts;

/// <summary>Trusted module/administrative reads, not human authorization or stock truth.</summary>
public interface IStockItemProjectionQueries
{
    Task<StockItemProjectionStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<StockItemProjectionView?> GetAsync(
        OrganizationId organizationId,
        Guid stockItemId,
        CancellationToken cancellationToken = default
    );
}
