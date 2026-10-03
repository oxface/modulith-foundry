using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Purchasing.Persistence;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

internal sealed class PurchaseOrderEventReader(PurchasingDbContext context)
{
    internal async Task<ReconstructedPurchaseOrder?> ReadAsync(
        Guid organizationId,
        Guid id,
        long? version,
        DateTimeOffset? recordedAt,
        CancellationToken cancellationToken
    )
    {
        var stream = await context
            .EventStreams.AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.OrganizationId == organizationId
                    && x.Id == id
                    && x.StreamType == PurchaseOrderStore.StreamType,
                cancellationToken
            );
        if (stream is null || version > stream.Version)
            return null;
        // Capture a committed head. Neither sequence allocation nor wall-clock cutoff defines commit order.
        var head = version ?? stream.Version;
        var query = context
            .Events.AsNoTracking()
            .Where(x =>
                x.OrganizationId == organizationId && x.StreamId == id && x.StreamVersion <= head
            );
        if (recordedAt is { } cutoff)
            query = query.Where(x => x.RecordedAt <= cutoff.ToUniversalTime());
        var events = await query.OrderBy(x => x.StreamVersion).ToListAsync(cancellationToken);
        if (events.Count == 0)
        {
            if (recordedAt is { } boundary && boundary < stream.CreatedAt)
                return null;
            throw new PurchaseOrderIntegrityException(id, PurchaseOrderIntegrityFailure.EventRange);
        }
        PurchaseOrderState? state = null;
        long ordinal = 0;
        DateTimeOffset? previous = null;
        foreach (var stored in events)
        {
            if (
                stored.StreamVersion != ++ordinal
                || (previous is { } timestamp && stored.RecordedAt < timestamp)
            )
                throw new PurchaseOrderIntegrityException(
                    id,
                    PurchaseOrderIntegrityFailure.EventRange
                );
            state = PurchaseOrderEvolution.Evolve(
                state,
                PurchaseOrderEventSerializer.Deserialize(stored)
            );
            previous = stored.RecordedAt;
        }
        if ((recordedAt is null || recordedAt >= stream.UpdatedAt) && ordinal != head)
            throw new PurchaseOrderIntegrityException(id, PurchaseOrderIntegrityFailure.EventRange);
        return new(state!, ordinal, previous!.Value);
    }
}

internal sealed record ReconstructedPurchaseOrder(
    PurchaseOrderState State,
    long Version,
    DateTimeOffset RecordedAt
);
