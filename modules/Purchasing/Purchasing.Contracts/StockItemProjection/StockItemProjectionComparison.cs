namespace ModulithFoundry.Modules.Purchasing.Contracts;

public sealed record StockItemProjectionComparison(
    long SnapshotWatermark,
    int Missing,
    int Different,
    int Unexpected,
    int PreservedNewer
)
{
    /// <summary>
    /// No observed differences among snapshot-covered rows. PreservedNewer rows are not verified
    /// by this snapshot; this is not a caught-up, queue-health or source-restore guarantee.
    /// </summary>
    public bool IsConsistent => Missing == 0 && Different == 0 && Unexpected == 0;
}
