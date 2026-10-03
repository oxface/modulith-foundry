namespace ModulithFoundry.Modules.Inventory.Contracts;

/// <summary>Trusted full-catalog bootstrap capability; never mapped to HTTP.</summary>
public interface IStockItemSnapshotExporter
{
    Task<StockItemSnapshotV1> ExportAsync(CancellationToken cancellationToken = default);
}
