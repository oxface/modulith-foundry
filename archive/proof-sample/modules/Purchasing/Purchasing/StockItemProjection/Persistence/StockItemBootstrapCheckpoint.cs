namespace ModulithFoundry.Modules.Purchasing.StockItemProjection.Persistence;

internal sealed class StockItemBootstrapCheckpoint
{
    internal int Id { get; private set; } = 1;
    internal bool IsReady { get; private set; }
    internal long? SnapshotWatermark { get; private set; }

    internal void Complete(long watermark)
    {
        SnapshotWatermark = watermark;
        IsReady = true;
    }
}
