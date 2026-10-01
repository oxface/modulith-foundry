namespace ModulithFoundry.Modules.Inventory.Contracts;

public interface IStockPositionOperations
{
    Task<CorrectStockQuantityResult> CorrectQuantityAsync(
        CorrectStockQuantityCommand command,
        CancellationToken cancellationToken = default);

    Task<RecordStockReceiptResult> RecordReceiptAsync(
        RecordStockReceiptCommand command,
        CancellationToken cancellationToken = default);

    Task<GetStockPositionResult> GetCurrentAsync(
        GetStockPositionQuery query,
        CancellationToken cancellationToken = default);

    Task<GetStockPositionResult> GetAtVersionAsync(
        GetStockPositionAtVersionQuery query,
        CancellationToken cancellationToken = default);

    Task<GetStockPositionResult> GetAsOfAsync(
        GetStockPositionAsOfQuery query,
        CancellationToken cancellationToken = default);

    Task<GetStockPositionHistoryResult> GetHistoryAsync(
        GetStockPositionHistoryQuery query,
        CancellationToken cancellationToken = default);
}
