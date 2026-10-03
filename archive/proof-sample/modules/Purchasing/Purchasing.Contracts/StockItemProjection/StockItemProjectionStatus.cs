namespace ModulithFoundry.Modules.Purchasing.Contracts;

public sealed record StockItemProjectionStatus(bool IsReady, long? SnapshotWatermark);
