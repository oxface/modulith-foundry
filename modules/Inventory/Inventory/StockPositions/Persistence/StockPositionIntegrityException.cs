namespace ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

internal sealed class StockPositionIntegrityException(
    Guid streamId,
    StockPositionIntegrityFailure failure,
    long? expectedVersion = null,
    long? observedVersion = null,
    Exception? innerException = null
)
    : Exception(
        $"Stock Position stream '{streamId}' failed integrity validation: {failure}; "
            + $"expected version {expectedVersion}, observed version {observedVersion}.",
        innerException
    )
{
    internal Guid StreamId { get; } = streamId;

    internal StockPositionIntegrityFailure Failure { get; } = failure;

    internal long? ExpectedVersion { get; } = expectedVersion;

    internal long? ObservedVersion { get; } = observedVersion;
}

internal enum StockPositionIntegrityFailure
{
    WriteModelMissing = 1,
    WriteModelBehind = 2,
    HistoryGap = 3,
    HistoryVersionMismatch = 4,
    UnknownEvent = 5,
    InvalidEventPayload = 6,
    InvalidEventSequence = 7,
    RecordedTimeRegression = 8,
    StreamMissing = 9,
    WriteModelIdentityMismatch = 10,
}
