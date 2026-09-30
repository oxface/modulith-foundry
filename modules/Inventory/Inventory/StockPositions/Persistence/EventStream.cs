using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

internal sealed class EventStream : IOrganizationOwned
{
    internal const string StockPositionStreamType = "inventory.stock-position";

    private EventStream()
    {
        StreamType = null!;
    }

    private EventStream(
        Guid id,
        Guid organizationId,
        Guid stockItemId,
        Guid stockingLocationId,
        string streamType,
        long version,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrganizationId = organizationId;
        StockItemId = stockItemId;
        StockingLocationId = stockingLocationId;
        StreamType = streamType;
        Version = version;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    internal Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    internal Guid StockItemId { get; private set; }

    internal Guid StockingLocationId { get; private set; }

    internal string StreamType { get; private set; }

    internal long Version { get; private set; }

    internal DateTimeOffset CreatedAt { get; private set; }

    internal DateTimeOffset UpdatedAt { get; private set; }

    internal static EventStream Open(
        Guid id,
        Guid organizationId,
        Guid stockItemId,
        Guid stockingLocationId,
        long version,
        DateTimeOffset createdAt) =>
        new(id, organizationId, stockItemId, stockingLocationId,
            StockPositionStreamType, version, createdAt);

    internal void Advance(long expectedVersion, long newVersion, DateTimeOffset updatedAt)
    {
        if (Version != expectedVersion || newVersion <= expectedVersion)
        {
            throw new InvalidOperationException("Event stream version does not match the append.");
        }

        Version = newVersion;
        UpdatedAt = updatedAt;
    }
}
