using System.Text.Json;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Purchasing.PurchaseOrders.Events;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

internal sealed class PurchaseOrderSummary : IOrganizationOwned
{
    private PurchaseOrderSummary() { }

    internal Guid StreamId { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal string Code { get; private set; } = null!;
    internal string Currency { get; private set; } = null!;
    internal bool IsIssued { get; private set; }
    internal int LineCount { get; private set; }
    internal decimal Total { get; private set; }
    internal long Version { get; private set; }
    internal JsonElement LineAmounts { get; private set; }

    internal static PurchaseOrderSummary Create(Guid id, Guid organizationId) =>
        new()
        {
            StreamId = id,
            OrganizationId = organizationId,
            LineAmounts = JsonSerializer.SerializeToElement(new Dictionary<string, decimal>()),
        };

    internal void Apply(IPurchaseOrderEvent @event)
    {
        switch (@event)
        {
            case PurchaseOrderDrafted draft:
                Code = draft.Code;
                Currency = draft.Currency;
                break;
            case PurchaseOrderLineSet line:
                var amounts =
                    LineAmounts.Deserialize<Dictionary<string, decimal>>()
                    ?? throw new PurchaseOrderIntegrityException(
                        StreamId,
                        PurchaseOrderIntegrityFailure.InvalidSummary
                    );
                amounts[line.ItemCode] = line.Quantity * line.UnitPrice;
                LineCount = amounts.Count;
                Total = amounts.Values.Sum();
                LineAmounts = JsonSerializer.SerializeToElement(amounts);
                break;
            case PurchaseOrderIssued:
                IsIssued = true;
                break;
            default:
                throw new PurchaseOrderIntegrityException(
                    StreamId,
                    PurchaseOrderIntegrityFailure.UnknownEvent
                );
        }
        Version++;
    }
}
