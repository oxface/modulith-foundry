using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.Modules.Sales.Fulfilment;

internal sealed record ReservationDecision(
    OrderFulfilmentLineStatus Status,
    Guid? ReservationId,
    decimal AvailableQuantity,
    string? ReasonCode,
    string Fingerprint
);
