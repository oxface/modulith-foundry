using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.StockPositions.Queries;
using ModulithFoundry.Modules.Inventory.StockPositions.RecordStockReceipt;

namespace ModulithFoundry.Modules.Inventory.StockPositions;

internal sealed class StockPositionOperations(
    RecordStockReceiptHandler recordReceipt,
    StockPositionQueries queries) : IStockPositions
{
    public Task<RecordStockReceiptResult> RecordReceiptAsync(
        RecordStockReceiptCommand command,
        CancellationToken cancellationToken = default) =>
        recordReceipt.HandleAsync(command, cancellationToken);

    public Task<GetStockPositionResult> GetCurrentAsync(
        GetStockPositionQuery query,
        CancellationToken cancellationToken = default) =>
        queries.GetCurrentAsync(query, cancellationToken);
}
