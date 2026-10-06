using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Samples.Wholesale.Purchasing.Contracts;

namespace ModulithFoundry.Samples.Wholesale.Purchasing.PurchaseOrders;

internal sealed class PurchaseOrderQueries(
    PurchasingDbContext database,
    PurchaseOrderInlineProjection projection
) : IPurchaseOrderQueries
{
    public async Task<PurchaseOrderHistory?> ReadCurrentAsync(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        var views = await ReadAsync(id, cancellationToken);
        return views is { } rows
            ? rows.Current.ReadState().ToHistory(id, rows.Current.Version, rows.Current.RecordedAt)
            : null;
    }

    public async Task<PurchaseOrderSummary?> ReadSummaryAsync(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        var views = await ReadAsync(id, cancellationToken);
        return views?.Summary.ToContract();
    }

    private async Task<(
        PurchaseOrderCurrentRow Current,
        PurchaseOrderSummaryRow Summary
    )?> ReadAsync(Guid id, CancellationToken cancellationToken)
    {
        _ = database.RequiredOrganizationKey;
        var stream = await database
            .EventStreams.AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.Id == id && row.StreamType == PurchaseOrderHistoryReader.StreamType,
                cancellationToken
            );
        return stream is null ? null : await projection.LoadAsync(stream, cancellationToken);
    }
}
