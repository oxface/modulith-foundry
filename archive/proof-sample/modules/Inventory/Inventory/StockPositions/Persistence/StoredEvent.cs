using System.Text.Json;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

internal sealed class StoredEvent : IOrganizationOwned
{
    private StoredEvent()
    {
        EventName = null!;
    }

    private StoredEvent(
        Guid eventId,
        Guid organizationId,
        Guid streamId,
        long streamVersion,
        string eventName,
        int schemaVersion,
        DateTimeOffset recordedAt,
        JsonElement payload,
        JsonElement metadata
    )
    {
        EventId = eventId;
        OrganizationId = organizationId;
        StreamId = streamId;
        StreamVersion = streamVersion;
        EventName = eventName;
        SchemaVersion = schemaVersion;
        RecordedAt = recordedAt;
        Payload = payload;
        Metadata = metadata;
    }

    internal long GlobalSequence { get; private set; }

    internal Guid EventId { get; private set; }

    public Guid OrganizationId { get; private set; }

    internal Guid StreamId { get; private set; }

    internal long StreamVersion { get; private set; }

    internal string EventName { get; private set; }

    internal int SchemaVersion { get; private set; }

    internal DateTimeOffset RecordedAt { get; private set; }

    internal JsonElement Payload { get; private set; }

    internal JsonElement Metadata { get; private set; }

    internal static StoredEvent Create(
        Guid eventId,
        Guid organizationId,
        Guid streamId,
        long streamVersion,
        string eventName,
        int schemaVersion,
        DateTimeOffset recordedAt,
        JsonElement payload,
        JsonElement metadata
    ) =>
        new(
            eventId,
            organizationId,
            streamId,
            streamVersion,
            eventName,
            schemaVersion,
            recordedAt,
            payload,
            metadata
        );
}
