using Microsoft.EntityFrameworkCore;

namespace ModulithFoundry.EventSourcing.EntityFrameworkCore;

/// <summary>Prepares an aggregate's pending facts inside a caller-owned native transaction.</summary>
public sealed class EventAppender<TEvent, TStream, TStoredEvent>
    where TEvent : class
    where TStream : class, IEventStreamRecord
    where TStoredEvent : class, IStoredEventRecord
{
    private readonly DbContext database;
    private readonly EventRecordAdapter<TEvent, TStream, TStoredEvent> records;
    private readonly TimeProvider timeProvider;
    private readonly string streamType;

    public EventAppender(
        DbContext database,
        EventRecordAdapter<TEvent, TStream, TStoredEvent> records,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(records.StreamType);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(records.StreamType.Length, 100);
        this.database = database;
        this.records = records;
        this.timeProvider = timeProvider;
        streamType = records.StreamType;
    }

    /// <summary>Prepares encoding, identities, positions and one recorded time without tracking or changing the header.</summary>
    public PreparedEventAppend<TStream, TStoredEvent> Prepare(
        TStream observedStream,
        IEventSourcedAggregate<TEvent> aggregate,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(observedStream);
        ArgumentNullException.ThrowIfNull(aggregate);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfNegative(aggregate.ExpectedVersion);
        if (aggregate.Id != observedStream.Id || observedStream.StreamType != streamType)
            throw new ArgumentException(
                "The aggregate identity and configured family must match the observed stream.",
                nameof(aggregate)
            );
        TEvent[] batch = aggregate.PendingEvents.ToArray();
        ArgumentOutOfRangeException.ThrowIfLessThan(batch.Length, 1);
        foreach (TEvent @event in batch)
            ArgumentNullException.ThrowIfNull(@event);
        long nextVersion = checked(aggregate.ExpectedVersion + batch.Length);
        if (aggregate.Version != nextVersion)
            throw new InvalidOperationException(
                "The aggregate version must include exactly its pending facts."
            );
        var append = new PreparedEventAppend<TStream, TStoredEvent>(
            database,
            observedStream,
            aggregate,
            batch,
            nextVersion,
            timeProvider.GetUtcNow()
        );
        var rows = new HashSet<TStoredEvent>(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < batch.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TStoredEvent row = records.CreateRecord(batch[index], observedStream);
            ArgumentNullException.ThrowIfNull(row);
            if (!rows.Add(row))
                throw new InvalidOperationException(
                    "The adapter must return a fresh row for each fact."
                );
            ArgumentException.ThrowIfNullOrWhiteSpace(row.EventName);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(row.EventName.Length, 200);
            ArgumentOutOfRangeException.ThrowIfLessThan(row.SchemaVersion, 1);
            if (
                row.Payload.ValueKind
                is System.Text.Json.JsonValueKind.Undefined
                    or System.Text.Json.JsonValueKind.Null
            )
                throw new ArgumentException(
                    "The record adapter must supply a nonnull JSON event payload."
                );
            append.PrepareRow(row, index);
        }
        append.RequireUnchanged();
        cancellationToken.ThrowIfCancellationRequested();
        return append;
    }
}
