using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Orders;

namespace ModulithFoundry.Modules.Sales.Fulfilment;

internal sealed class OrderFulfilmentProcess : IOrganizationOwned
{
    private OrderFulfilmentProcess() { }

    internal Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal Guid OrderId { get; private set; }
    internal OrderFulfilmentStatus Status { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }

    internal static OrderFulfilmentProcess Start(SalesOrder order, DateTimeOffset createdAt)
    {
        if (order.Status != SalesOrderStatus.Approved)
            throw new InvalidOperationException("Only an approved order starts fulfilment.");
        return new()
        {
            Id = Guid.CreateVersion7(createdAt),
            OrganizationId = order.OrganizationId,
            OrderId = order.Id,
            Status = OrderFulfilmentStatus.PendingDispatch,
            CreatedAt = createdAt,
        };
    }
}
