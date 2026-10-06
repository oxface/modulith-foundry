using Microsoft.EntityFrameworkCore;

namespace ModulithFoundry.Samples.Wholesale.Purchasing.PurchaseOrders;

internal sealed class PurchaseOrderInlineProjection(PurchasingDbContext database)
{
    internal async Task<(
        PurchaseOrderCurrentRow Current,
        PurchaseOrderSummaryRow Summary
    )> LoadAsync(EventStream stream, CancellationToken cancellationToken)
    {
        if (stream.Version < 1 || stream.CreatedAt > stream.UpdatedAt)
            throw new InvalidDataException("The purchase-order stream header is invalid.");
        var current = await database
            .PurchaseOrders.AsNoTracking()
            .SingleOrDefaultAsync(row => row.StreamId == stream.Id, cancellationToken);
        var summary = await database
            .PurchaseOrderSummaries.AsNoTracking()
            .SingleOrDefaultAsync(row => row.StreamId == stream.Id, cancellationToken);
        if (
            current is null
            || summary is null
            || current.Version < stream.Version
            || summary.Version < stream.Version
        )
            throw new InvalidDataException(
                "A required purchase-order view is missing or behind its stream."
            );
        if (current.Version > stream.Version || summary.Version > stream.Version)
            throw new DbUpdateConcurrencyException(
                "A purchase-order view advanced after the header read."
            );
        if (current.RecordedAt != stream.UpdatedAt || summary.RecordedAt != stream.UpdatedAt)
            throw new InvalidDataException(
                "The purchase-order views and stream timestamps disagree."
            );
        return (current, summary);
    }

    internal void Stage(
        PurchaseOrderCurrentRow? current,
        PurchaseOrderCurrentRow next,
        PurchaseOrderSummaryRow? summary,
        PurchaseOrderSummaryRow nextSummary
    )
    {
        if (current is null)
            database.PurchaseOrders.Add(next);
        else
        {
            database.PurchaseOrders.Attach(current);
            database.Entry(current).CurrentValues.SetValues(next);
        }
        if (summary is null)
            database.PurchaseOrderSummaries.Add(nextSummary);
        else
        {
            database.PurchaseOrderSummaries.Attach(summary);
            database.Entry(summary).CurrentValues.SetValues(nextSummary);
        }
    }
}
