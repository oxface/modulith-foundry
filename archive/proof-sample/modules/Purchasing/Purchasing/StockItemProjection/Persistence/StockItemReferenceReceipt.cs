using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Purchasing.StockItemProjection.Persistence;

internal sealed class StockItemReferenceReceipt : IOrganizationOwned
{
    private StockItemReferenceReceipt()
    {
        Fingerprint = null!;
    }

    internal Guid MessageId { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal string Fingerprint { get; private set; }
    internal DateTimeOffset ProcessedAt { get; private set; }

    internal static StockItemReferenceReceipt Processed(
        Guid messageId,
        Guid organizationId,
        string fingerprint,
        DateTimeOffset now
    ) =>
        new()
        {
            MessageId = messageId,
            OrganizationId = organizationId,
            Fingerprint = fingerprint,
            ProcessedAt = now,
        };
}
