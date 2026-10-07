namespace ModulithFoundry.EventSourcing.EntityFrameworkCore;

/// <summary>A configured aggregate write store. The caller owns the native transaction and save.</summary>
public interface IEventStore<TAggregate>
    where TAggregate : class
{
    Task<TAggregate?> GetForWritingAsync(
        Guid id,
        long? expectedVersion = null,
        CancellationToken cancellationToken = default
    );

    Task<EventAppendResult> AppendAsync(
        TAggregate aggregate,
        CancellationToken cancellationToken = default
    );
}

/// <summary>Metadata of a staged batch, not confirmation of a commit.</summary>
public sealed record EventAppendResult(long Version, DateTimeOffset RecordedAt);
