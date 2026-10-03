namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

internal enum PurchaseOrderIntegrityFailure
{
    EventOrder = 1,
    InvalidWriteModel = 2,
    InvalidSummary = 3,
    UnknownEvent = 4,
    InvalidEventPayload = 5,
    RecordedTimeRegression = 6,
    UnexpectedInlineModel = 7,
    InlineModelBehind = 8,
    WriteModelBehind = 9,
    EventRange = 10,
    EmptyState = 11,
}
