using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Orders;

namespace ModulithFoundry.Modules.Sales.Fulfilment;

internal sealed class OrderFulfilmentLine
{
    private OrderFulfilmentLine()
    {
        BaseUnitCode = null!;
    }

    internal int LineNumber { get; private set; }
    internal Guid StockItemId { get; private set; }
    internal decimal Quantity { get; private set; }
    internal string BaseUnitCode { get; private set; }
    internal Guid OperationId { get; private set; }
    internal Guid CommandMessageId { get; private set; }
    internal OrderFulfilmentLineStatus Status { get; private set; }
    internal Guid? ReservationId { get; private set; }
    internal decimal? AvailableQuantity { get; private set; }
    internal string? ReasonCode { get; private set; }
    internal string? OutcomeFingerprint { get; private set; }
    internal int AttemptCount { get; private set; }
    internal DateTimeOffset? ResponseDeadline { get; private set; }

    internal static OrderFulfilmentLine Start(SalesOrderLine line, DateTimeOffset now) =>
        new()
        {
            LineNumber = line.LineNumber,
            StockItemId = line.StockItemId,
            Quantity = line.Quantity,
            BaseUnitCode = line.BaseUnitCode,
            OperationId = Guid.CreateVersion7(now),
            CommandMessageId = Guid.CreateVersion7(now),
            Status = OrderFulfilmentLineStatus.PendingReservation,
        };

    internal void Queue(DateTimeOffset now)
    {
        if (AttemptCount != 0)
            throw new InvalidOperationException("Reservation intent was already queued.");
        AttemptCount = 1;
        ResponseDeadline = now.AddMinutes(5);
    }

    internal void Complete(ReservationDecision decision)
    {
        if (Status != OrderFulfilmentLineStatus.PendingReservation)
            throw new InvalidOperationException("A reservation line already has its outcome.");
        bool valid = decision.Status switch
        {
            OrderFulfilmentLineStatus.Reserved => decision.ReservationId is { } id
                && id != Guid.Empty
                && decision.ReasonCode is null,
            OrderFulfilmentLineStatus.Shortage or OrderFulfilmentLineStatus.Rejected =>
                decision.ReservationId is null && !string.IsNullOrWhiteSpace(decision.ReasonCode),
            _ => false,
        };
        if (
            !valid
            || decision.AvailableQuantity < 0
            || string.IsNullOrWhiteSpace(decision.Fingerprint)
        )
            throw new InvalidOperationException("Reservation decision is invalid.");
        Status = decision.Status;
        ReservationId = decision.ReservationId;
        AvailableQuantity = decision.AvailableQuantity;
        ReasonCode = decision.ReasonCode;
        OutcomeFingerprint = decision.Fingerprint;
        ResponseDeadline = null;
    }
}
