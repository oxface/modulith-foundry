namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

internal sealed class PurchaseOrderIntegrityException(
    Guid? streamId,
    PurchaseOrderIntegrityFailure failure,
    Exception? innerException = null
) : Exception("Purchase Order persistence integrity failure.", innerException)
{
    internal Guid? StreamId { get; } = streamId;
    internal PurchaseOrderIntegrityFailure Failure { get; } = failure;
}

internal sealed class PurchaseOrderConcurrencyException : Exception;
