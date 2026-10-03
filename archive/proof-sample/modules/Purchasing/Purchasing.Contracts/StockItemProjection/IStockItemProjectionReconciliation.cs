namespace ModulithFoundry.Modules.Purchasing.Contracts;

/// <summary>Trusted, non-HTTP administration of the consumer-owned reference projection.</summary>
public interface IStockItemProjectionReconciliation
{
    Task<StockItemProjectionComparison> InspectAsync(CancellationToken cancellationToken = default);

    /// <summary>Repairs snapshot-covered differences; returns the comparison before repair.</summary>
    Task<StockItemProjectionComparison> RepairAsync(CancellationToken cancellationToken = default);
}
