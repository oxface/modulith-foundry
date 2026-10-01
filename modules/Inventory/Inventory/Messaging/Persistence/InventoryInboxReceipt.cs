using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Messaging.Persistence;

internal sealed class InventoryInboxReceipt : IOrganizationOwned
{
    private InventoryInboxReceipt()
    {
        Fingerprint = null!;
    }

    internal Guid MessageId { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal string Fingerprint { get; private set; }
    internal DateTimeOffset ProcessedAt { get; private set; }

    internal static InventoryInboxReceipt Processed(
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
