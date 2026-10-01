namespace ModulithFoundry.Modules.Inventory.Contracts;

/// <summary>Administrative full replay and atomic replacement of a Stock Position's required write model.</summary>
public interface IStockPositionProjectionRebuilder
{
    /// <remarks>
    /// Requires current organization context and projection-rebuild permission.
    /// Failure or cancellation before commit leaves the serving model unchanged.
    /// Retry in a fresh scope replays from the beginning; no durable progress is kept.
    /// </remarks>
    Task<StockPositionRebuildResult> RebuildAsync(RebuildStockPositionProjectionCommand command, CancellationToken cancellationToken = default);
}
