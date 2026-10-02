using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Orders;

namespace ModulithFoundry.Modules.Sales.Fulfilment;

internal sealed class OrderFulfilmentProcess : IOrganizationOwned
{
    internal const string SystemActor = "sales.order-fulfilment";
    private readonly List<OrderFulfilmentLine> _lines = [];

    private OrderFulfilmentProcess() { }

    internal Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal Guid OrderId { get; private set; }
    internal OrderFulfilmentStatus Status { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal long Version { get; private set; } = 1;
    internal long OrderNumber { get; private set; }
    internal Guid? StockingLocationId { get; private set; }
    internal bool CancellationRequested { get; private set; }
    internal IReadOnlyCollection<OrderFulfilmentLine> Lines => _lines;

    internal void RequestCancellation()
    {
        if (CancellationRequested)
            return;
        CancellationRequested = true;
        Version = checked(Version + 1);
        RefreshStatus();
    }

    internal bool QueueRelease(OrderFulfilmentLine line, DateTimeOffset now)
    {
        if (!_lines.Contains(line))
            throw new InvalidOperationException("The line does not belong to this process.");
        if (!CancellationRequested || !line.QueueRelease(now))
            return false;
        Version = checked(Version + 1);
        RefreshStatus();
        return true;
    }

    internal void ApplyRelease(
        OrderFulfilmentLine line,
        OrderFulfilmentReleaseStatus status,
        string? reason,
        string fingerprint
    )
    {
        if (!_lines.Contains(line) || !CancellationRequested)
            throw new InvalidOperationException(
                "Release requires this process's cancelled demand."
            );
        line.CompleteRelease(status, reason, fingerprint);
        Version = checked(Version + 1);
        RefreshStatus();
    }

    internal static OrderFulfilmentProcess Start(SalesOrder order, DateTimeOffset createdAt)
    {
        if (order.Status != SalesOrderStatus.Approved)
            throw new InvalidOperationException("Only an approved order starts fulfilment.");
        var process = new OrderFulfilmentProcess
        {
            Id = Guid.CreateVersion7(createdAt),
            OrganizationId = order.OrganizationId,
            OrderId = order.Id,
            Status = OrderFulfilmentStatus.PendingDispatch,
            CreatedAt = createdAt,
            OrderNumber = order.OrderNumber,
        };
        process._lines.AddRange(
            order.Lines.Select(line => OrderFulfilmentLine.Start(line, createdAt))
        );
        return process;
    }

    internal bool QueueReservations(SalesOrder order, Guid locationId, DateTimeOffset now)
    {
        if (Status != OrderFulfilmentStatus.PendingDispatch)
            return false;
        if (
            order.Id != OrderId
            || order.OrganizationId != OrganizationId
            || order.Status != SalesOrderStatus.Approved
        )
            throw new InvalidOperationException("Fulfilment must use its approved order.");
        ArgumentOutOfRangeException.ThrowIfEqual(locationId, Guid.Empty);
        // Existing pre-messaging processes had no line state. Initialize once under the process version check.
        if (_lines.Count == 0)
            _lines.AddRange(order.Lines.Select(line => OrderFulfilmentLine.Start(line, now)));
        OrderNumber = order.OrderNumber;
        StockingLocationId = locationId;
        Status = OrderFulfilmentStatus.AwaitingReservations;
        Version = checked(Version + 1);
        foreach (OrderFulfilmentLine line in _lines)
            line.Queue(now);
        return true;
    }

    internal void ApplyOutcome(OrderFulfilmentLine line, ReservationDecision decision)
    {
        if (!_lines.Contains(line))
            throw new InvalidOperationException("The line does not belong to this process.");
        line.Complete(decision);
        Version = checked(Version + 1);
        RefreshStatus();
    }

    internal bool QueueReplenishment(OrderFulfilmentLine line, DateTimeOffset now)
    {
        if (!_lines.Contains(line))
            throw new InvalidOperationException("The line does not belong to this process.");
        if (CancellationRequested || !line.QueueReplenishment(now))
            return false;
        Version = checked(Version + 1);
        return true;
    }

    internal void ApplyReplenishment(
        OrderFulfilmentLine line,
        Guid? requirementId,
        long? number,
        string? reason,
        string fingerprint
    )
    {
        if (!_lines.Contains(line))
            throw new InvalidOperationException("The line does not belong to this process.");
        line.CompleteReplenishment(requirementId, number, reason, fingerprint);
        Version = checked(Version + 1);
        RefreshStatus();
    }

    private void RefreshStatus() =>
        Status =
            CancellationRequested ? GetCompensationStatus()
            : _lines.Any(item => item.Status == OrderFulfilmentLineStatus.Rejected)
            || _lines.Any(item => item.ReplenishmentReasonCode != null)
                ? OrderFulfilmentStatus.AttentionRequired
            : _lines.Any(item => item.Status == OrderFulfilmentLineStatus.PendingReservation)
                ? OrderFulfilmentStatus.AwaitingReservations
            : _lines.Any(item => item.Status == OrderFulfilmentLineStatus.Shortage)
                ? OrderFulfilmentStatus.AwaitingReplenishment
            : OrderFulfilmentStatus.Reserved;

    private OrderFulfilmentStatus GetCompensationStatus()
    {
        if (_lines.Any(item => item.ReleaseStatus == OrderFulfilmentReleaseStatus.Rejected))
            return OrderFulfilmentStatus.AttentionRequired;
        bool waiting = _lines.Any(item =>
            (item.AttemptCount > 0 && item.Status == OrderFulfilmentLineStatus.PendingReservation)
            || (
                item.Status == OrderFulfilmentLineStatus.Reserved
                && item.ReleaseStatus
                    is not (
                        OrderFulfilmentReleaseStatus.Released
                        or OrderFulfilmentReleaseStatus.AlreadyReleased
                    )
            )
        );
        return waiting
            ? OrderFulfilmentStatus.CompensationPending
            : OrderFulfilmentStatus.Compensated;
    }
}
