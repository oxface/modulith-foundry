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
    internal IReadOnlyCollection<OrderFulfilmentLine> Lines => _lines;

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
        Status =
            _lines.Any(item => item.Status == OrderFulfilmentLineStatus.Rejected)
                ? OrderFulfilmentStatus.AttentionRequired
            : _lines.Any(item => item.Status == OrderFulfilmentLineStatus.PendingReservation)
                ? OrderFulfilmentStatus.AwaitingReservations
            : _lines.Any(item => item.Status == OrderFulfilmentLineStatus.Shortage)
                ? OrderFulfilmentStatus.AwaitingReplenishment
            : OrderFulfilmentStatus.Reserved;
    }
}
