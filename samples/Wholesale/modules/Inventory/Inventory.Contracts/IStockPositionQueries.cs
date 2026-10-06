namespace ModulithFoundry.Samples.Wholesale.Inventory.Contracts;

public interface IStockPositionQueries
{
    Task<StockPositionHistory?> ReadCurrentAsync(Guid id, CancellationToken cancellationToken);
}
