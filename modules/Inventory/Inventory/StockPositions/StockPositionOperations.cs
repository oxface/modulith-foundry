using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.StockPositions.CorrectStockQuantity;
using ModulithFoundry.Modules.Inventory.StockPositions.Queries;
using ModulithFoundry.Modules.Inventory.StockPositions.RecordStockReceipt;

namespace ModulithFoundry.Modules.Inventory.StockPositions;

internal sealed class StockPositionOperations(
    CorrectStockQuantityHandler correctQuantity,
    RecordStockReceiptHandler recordReceipt,
    StockPositionQueries queries,
    StockPositionTemporalQueries temporalQueries,
    StockPositionHistoryQueries historyQueries) : IStockPositionOperations
{
    public Task<CorrectStockQuantityResult> CorrectQuantityAsync(
        CorrectStockQuantityCommand command,
        CancellationToken cancellationToken = default) =>
        correctQuantity.HandleAsync(command, cancellationToken);

    public Task<RecordStockReceiptResult> RecordReceiptAsync(
        RecordStockReceiptCommand command,
        CancellationToken cancellationToken = default) =>
        recordReceipt.HandleAsync(command, cancellationToken);

    public Task<GetStockPositionResult> GetCurrentAsync(
        GetStockPositionQuery query,
        CancellationToken cancellationToken = default) =>
        queries.GetCurrentAsync(query, cancellationToken);

    public Task<GetStockPositionResult> GetAtVersionAsync(
        GetStockPositionAtVersionQuery query,
        CancellationToken cancellationToken = default) => temporalQueries.GetAtVersionAsync(query, cancellationToken);

    public Task<GetStockPositionResult> GetAsOfAsync(
        GetStockPositionAsOfQuery query,
        CancellationToken cancellationToken = default) => temporalQueries.GetAsOfAsync(query, cancellationToken);

    public Task<GetStockPositionHistoryResult> GetHistoryAsync(
        GetStockPositionHistoryQuery query,
        CancellationToken cancellationToken = default) => historyQueries.GetAsync(query, cancellationToken);
}
