using System.Text.Json;
using ModulithFoundry.EventSourcing.EntityFrameworkCore;
using ModulithFoundry.Samples.Wholesale.Purchasing.Contracts;

namespace ModulithFoundry.Samples.Wholesale.Purchasing.PurchaseOrders;

internal sealed class PurchaseOrderSummaryRow : IInlineStateRecord
{
    public string OrganizationKey { get; set; } = null!;
    public Guid StreamId { get; set; }
    public long Version { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public string Code { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public int LineCount { get; set; }
    public decimal Total { get; set; }
    public JsonElement LineAmounts { get; set; }

    internal PurchaseOrderSummary ToContract() =>
        new(StreamId, Version, RecordedAt, Code, Currency, LineCount, Total);

    internal static PurchaseOrderSummaryRow Prepare(
        PurchaseOrderSummaryRow? current,
        string owner,
        Guid id,
        long version,
        DateTimeOffset recordedAt,
        IEnumerable<IPurchaseOrderEvent> events
    )
    {
        var row = Evolve(current, events);
        row.OrganizationKey = owner;
        row.StreamId = id;
        row.Version = version;
        row.RecordedAt = recordedAt;
        return row;
    }

    // This view evolves its own committed amounts, without reading the proposed aggregate state.
    internal static PurchaseOrderSummaryRow Evolve(
        PurchaseOrderSummaryRow? current,
        IEnumerable<IPurchaseOrderEvent> events
    )
    {
        var amounts = current is null
            ? new Dictionary<string, decimal>(StringComparer.Ordinal)
            : current.LineAmounts.Deserialize<Dictionary<string, decimal>>()
                ?? throw new InvalidDataException(
                    "The purchase-order summary amounts are unreadable."
                );
        if (
            current is not null
            && (
                amounts.Count != current.LineCount
                || amounts.Values.Any(amount => amount < 0)
                || amounts.Values.Sum() != current.Total
            )
        )
            throw new InvalidDataException(
                "The purchase-order summary amounts disagree with its columns."
            );
        string? code = current?.Code;
        string? currency = current?.Currency;
        foreach (var @event in events)
        {
            switch (@event)
            {
                case PurchaseOrderDrafted drafted when code is null:
                    code = drafted.Code;
                    currency = drafted.Currency;
                    break;
                case PurchaseOrderLineSet line when code is not null:
                    amounts[line.ItemCode] = line.Quantity * line.UnitPrice;
                    break;
                default:
                    throw new InvalidOperationException(
                        "The summary cannot evolve this event sequence."
                    );
            }
        }
        return new PurchaseOrderSummaryRow
        {
            Code = code ?? throw new InvalidOperationException("The summary has not been drafted."),
            Currency =
                currency ?? throw new InvalidOperationException("The summary has no currency."),
            LineCount = amounts.Count,
            Total = amounts.Values.Sum(),
            LineAmounts = JsonSerializer.SerializeToElement(amounts),
        };
    }
}
