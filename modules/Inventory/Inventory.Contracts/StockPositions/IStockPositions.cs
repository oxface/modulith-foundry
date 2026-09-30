namespace ModulithFoundry.Modules.Inventory.Contracts;

public interface IStockPositions
{
    Task<RecordStockReceiptResult> RecordReceiptAsync(
        RecordStockReceiptCommand command,
        CancellationToken cancellationToken = default);

    Task<GetStockPositionResult> GetCurrentAsync(
        GetStockPositionQuery query,
        CancellationToken cancellationToken = default);
}
