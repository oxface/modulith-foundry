namespace ModulithFoundry.Modules.Sales.Fulfilment;

internal sealed class FulfilmentIntegrityException(Guid orderId)
    : Exception($"Approved Sales Order '{orderId}' has no fulfilment process.")
{
    internal Guid OrderId { get; } = orderId;
}
