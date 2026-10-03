using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Purchasing.Messaging.Persistence;

internal sealed class PurchasingInboxReceipt : IOrganizationOwned
{
    private PurchasingInboxReceipt() => Fingerprint = null!;

    internal Guid MessageId { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal string Fingerprint { get; private set; }
    internal bool Rejected { get; private set; }
    internal DateTimeOffset ProcessedAt { get; private set; }

    internal static PurchasingInboxReceipt Processed(
        Guid messageId,
        Guid organizationId,
        string fingerprint,
        DateTimeOffset now,
        bool rejected = false
    ) =>
        new()
        {
            MessageId = messageId,
            OrganizationId = organizationId,
            Fingerprint = fingerprint,
            ProcessedAt = now,
            Rejected = rejected,
        };
}
