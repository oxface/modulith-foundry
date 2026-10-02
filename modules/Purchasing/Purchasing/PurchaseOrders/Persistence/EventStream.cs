using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

internal sealed class EventStream : IOrganizationOwned
{
    private EventStream() { }

    internal Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal string StreamType { get; private set; } = null!;
    internal long Version { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }

    internal static EventStream Open(
        Guid id,
        Guid organizationId,
        string streamType,
        long version,
        DateTimeOffset now
    ) =>
        new()
        {
            Id = id,
            OrganizationId = organizationId,
            StreamType = streamType,
            Version = version,
            CreatedAt = now,
            UpdatedAt = now,
        };

    internal void Advance(long expectedVersion, long version, DateTimeOffset now)
    {
        if (Version != expectedVersion)
            throw new PurchaseOrderConcurrencyException();
        if (now < UpdatedAt)
            throw new PurchaseOrderIntegrityException(
                Id,
                PurchaseOrderIntegrityFailure.RecordedTimeRegression
            );
        Version = version;
        UpdatedAt = now;
    }
}
