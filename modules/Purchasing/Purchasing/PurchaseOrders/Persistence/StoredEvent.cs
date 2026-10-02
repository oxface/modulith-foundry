using System.Text.Json;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

internal sealed class StoredEvent : IOrganizationOwned
{
    private StoredEvent() { }

    internal Guid EventId { get; private set; }
    internal long GlobalSequence { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal Guid StreamId { get; private set; }
    internal long StreamVersion { get; private set; }
    internal string EventName { get; private set; } = null!;
    internal int SchemaVersion { get; private set; }
    internal DateTimeOffset RecordedAt { get; private set; }
    internal JsonElement Payload { get; private set; }
    internal JsonElement Metadata { get; private set; }

    internal static StoredEvent Create(
        Guid organizationId,
        Guid streamId,
        long version,
        string eventName,
        int schemaVersion,
        JsonElement payload,
        DateTimeOffset now,
        JsonElement metadata
    ) =>
        new()
        {
            EventId = Guid.CreateVersion7(now),
            OrganizationId = organizationId,
            StreamId = streamId,
            StreamVersion = version,
            EventName = eventName,
            SchemaVersion = schemaVersion,
            RecordedAt = now,
            Payload = payload,
            Metadata = metadata,
        };
}
