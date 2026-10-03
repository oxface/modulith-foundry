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
    internal Guid? ReplenishmentCommandMessageId { get; private set; }
    internal decimal? ReplenishmentQuantity { get; private set; }
    internal Guid? ReplenishmentRequirementId { get; private set; }
    internal long? ReplenishmentRequirementNumber { get; private set; }
    internal string? ReplenishmentReasonCode { get; private set; }
    internal string? ReplenishmentOutcomeFingerprint { get; private set; }
    internal Guid? ReleaseOperationId { get; private set; }
    internal Guid? ReleaseCommandMessageId { get; private set; }
    internal OrderFulfilmentReleaseStatus? ReleaseStatus { get; private set; }
    internal string? ReleaseReasonCode { get; private set; }
    internal string? ReleaseOutcomeFingerprint { get; private set; }
    internal DateTimeOffset? ReleaseResponseDeadline { get; private set; }

    internal bool QueueRelease(DateTimeOffset now)
    {
        if (Status != OrderFulfilmentLineStatus.Reserved || ReleaseCommandMessageId.HasValue)
            return false;
        ReleaseOperationId = Guid.CreateVersion7(now);
        ReleaseCommandMessageId = Guid.CreateVersion7(now);
        ReleaseStatus = OrderFulfilmentReleaseStatus.Pending;
        ReleaseResponseDeadline = now.AddMinutes(5);
        return true;
    }

    internal void CompleteRelease(
        OrderFulfilmentReleaseStatus status,
        string? reason,
        string fingerprint
    )
    {
        bool valid = status switch
        {
            OrderFulfilmentReleaseStatus.Released or OrderFulfilmentReleaseStatus.AlreadyReleased =>
                reason is null,
            OrderFulfilmentReleaseStatus.Rejected => !string.IsNullOrWhiteSpace(reason),
            _ => false,
        };
        if (
            ReleaseStatus != OrderFulfilmentReleaseStatus.Pending
            || !valid
            || string.IsNullOrWhiteSpace(fingerprint)
        )
            throw new InvalidOperationException("The line has no valid pending release decision.");
        ReleaseStatus = status;
        ReleaseReasonCode = reason;
        ReleaseOutcomeFingerprint = fingerprint;
        ReleaseResponseDeadline = null;
    }

    internal bool QueueReplenishment(DateTimeOffset now)
    {
        if (Status != OrderFulfilmentLineStatus.Shortage || ReplenishmentCommandMessageId.HasValue)
            return false;
        if (AvailableQuantity is not { } available || available < 0 || available >= Quantity)
            throw new InvalidOperationException("A shortage must retain a positive deficit.");
        ReplenishmentQuantity = Quantity - available;
        ReplenishmentCommandMessageId = Guid.CreateVersion7(now);
        return true;
    }

    internal void CompleteReplenishment(
        Guid? requirementId,
        long? number,
        string? reason,
        string fingerprint
    )
    {
        if (
            Status != OrderFulfilmentLineStatus.Shortage
            || !ReplenishmentCommandMessageId.HasValue
            || ReplenishmentOutcomeFingerprint is not null
        )
            throw new InvalidOperationException("The line has no pending replenishment request.");
        bool created =
            requirementId is { } id && id != Guid.Empty && number is > 0 && reason is null;
        bool rejected =
            requirementId is null && number is null && !string.IsNullOrWhiteSpace(reason);
        if ((!created && !rejected) || string.IsNullOrWhiteSpace(fingerprint))
            throw new InvalidOperationException("Replenishment decision is invalid.");
        ReplenishmentRequirementId = requirementId;
        ReplenishmentRequirementNumber = number;
        ReplenishmentReasonCode = reason;
        ReplenishmentOutcomeFingerprint = fingerprint;
    }

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
