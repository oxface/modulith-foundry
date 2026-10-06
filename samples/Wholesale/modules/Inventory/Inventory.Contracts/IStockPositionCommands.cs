namespace ModulithFoundry.Samples.Wholesale.Inventory.Contracts;

// Staging changes the caller's native context only. Staged is not committed success.
public interface IStockPositionCommands
{
    Task<StockPositionChangeResult> StageOpenAsync(
        OpenStockPosition request,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken
    );
    Task<StockPositionChangeResult> StageReceiptsAsync(
        ReceiveStock request,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken
    );
}

public sealed record OpenStockPosition(
    Guid Id,
    Guid StockItemId,
    Guid StockingLocationId,
    string BaseUnitCode,
    long ExpectedVersion = 0
);

public sealed record StockReceipt(decimal Quantity, string? DeliveryReference = null);

public sealed record ReceiveStock(
    Guid Id,
    long ExpectedVersion,
    IReadOnlyList<StockReceipt> Receipts
);

public abstract record StockPositionChangeResult
{
    public sealed record Staged(StockPositionHistory Proposed) : StockPositionChangeResult;

    public sealed record NotFound : StockPositionChangeResult;

    public sealed record Conflict : StockPositionChangeResult;
}
