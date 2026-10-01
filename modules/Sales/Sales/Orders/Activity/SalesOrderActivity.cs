using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.Modules.Sales.Orders.Activity;

internal sealed class SalesOrderActivity : IOrganizationOwned
{
    private SalesOrderActivity() { }

    private SalesOrderActivity(
        SalesOrder order,
        Guid actorUserId,
        SalesOrderActivityKind kind,
        DateTimeOffset occurredAt
    )
    {
        Id = Guid.CreateVersion7(occurredAt);
        OrganizationId = order.OrganizationId;
        OrderId = order.Id;
        ActorUserId = actorUserId;
        Kind = kind;
        OrderVersion = order.Version;
        OccurredAt = occurredAt;
    }

    internal Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal Guid OrderId { get; private set; }
    internal Guid? ActorUserId { get; private set; }
    internal string? SystemActor { get; private set; }
    internal long? ProcessVersion { get; private set; }
    internal int? LineNumber { get; private set; }
    internal SalesOrderActivityKind Kind { get; private set; }
    internal long OrderVersion { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }

    internal static SalesOrderActivity Record(
        SalesOrder order,
        Guid actorUserId,
        SalesOrderActivityKind kind,
        DateTimeOffset occurredAt
    ) => new(order, actorUserId, kind, occurredAt);

    internal static SalesOrderActivity RecordFulfilment(
        Fulfilment.OrderFulfilmentProcess process,
        long orderVersion,
        SalesOrderActivityKind kind,
        int? lineNumber,
        DateTimeOffset occurredAt
    ) =>
        new()
        {
            Id = Guid.CreateVersion7(occurredAt),
            OrganizationId = process.OrganizationId,
            OrderId = process.OrderId,
            Kind = kind,
            OrderVersion = orderVersion,
            OccurredAt = occurredAt,
            SystemActor = Fulfilment.OrderFulfilmentProcess.SystemActor,
            ProcessVersion = process.Version,
            LineNumber = lineNumber,
        };
}
