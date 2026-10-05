namespace ModulithFoundry.Samples.Wholesale.Inventory.Contracts;

public interface IStockCatalog
{
    Task<StockAvailability?> ReadAsync(string sku, CancellationToken cancellationToken);
}
