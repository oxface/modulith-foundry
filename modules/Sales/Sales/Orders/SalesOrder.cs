using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.Modules.Sales.Orders;

internal sealed class SalesOrder : IOrganizationOwned
{
    private readonly List<SalesOrderLine> _lines = [];

    private SalesOrder()
    {
        Currency = null!;
    }

    private SalesOrder(
        Guid id,
        Guid organizationId,
        Guid customerId,
        long number,
        string currency,
        DateTimeOffset createdAt
    )
    {
        Id = id;
        OrganizationId = organizationId;
        CustomerId = customerId;
        OrderNumber = number;
        Currency = currency;
        CreatedAt = createdAt;
    }

    internal Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal Guid CustomerId { get; private set; }
    internal long OrderNumber { get; private set; }
    internal string Currency { get; private set; }
    internal decimal TotalAmount { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal SalesOrderStatus Status { get; private set; } = SalesOrderStatus.Draft;
    internal long Version { get; private set; } = 1;
    internal Guid? SubmittedBy { get; private set; }
    internal DateTimeOffset? SubmittedAt { get; private set; }
    internal IReadOnlyCollection<SalesOrderLine> Lines => _lines;

    internal bool TrySubmit(Guid actorUserId, DateTimeOffset submittedAt)
    {
        if (Status != SalesOrderStatus.Draft)
        {
            return false;
        }

        Status = SalesOrderStatus.AwaitingApproval;
        SubmittedBy = actorUserId;
        SubmittedAt = submittedAt;
        Version++;
        return true;
    }

    internal static SalesOrder CreateDraft(
        Guid id,
        Guid organizationId,
        Guid customerId,
        long number,
        string currency,
        IReadOnlyList<SalesOrderLineInput> inputs,
        DateTimeOffset createdAt
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        string normalized = currency?.Trim().ToUpperInvariant() ?? "";
        if (normalized is not ("USD" or "EUR"))
        {
            throw new InvalidSalesOrderInputException(
                "currency",
                "The sample supports USD and EUR only."
            );
        }

        if (inputs.Count is < 1 or > 100)
        {
            throw new InvalidSalesOrderInputException(
                "lines",
                "An order must contain 1–100 lines."
            );
        }

        var order = new SalesOrder(id, organizationId, customerId, number, normalized, createdAt);
        for (int index = 0; index < inputs.Count; index++)
        {
            SalesOrderLine line = SalesOrderLine.Create(index + 1, inputs[index]);
            order._lines.Add(line);
            order.TotalAmount += line.LineAmount;
            if (order.TotalAmount > OrderAmount.Maximum)
            {
                throw new InvalidSalesOrderInputException(
                    "lines",
                    "The order total exceeds the supported range."
                );
            }
        }

        return order;
    }
}
