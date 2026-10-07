using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ModulithFoundry.EventSourcing.EntityFrameworkCore;

/// <summary>Shared native stream lookup, version checks and atomic batch/inline staging.</summary>
public abstract class EventStore<TAggregate, TEvent, TStream, TStoredEvent>
    : IEventStore<TAggregate>
    where TAggregate : class, IEventSourcedAggregate<TEvent>
    where TEvent : class
    where TStream : class, IEventStreamRecord
    where TStoredEvent : class, IStoredEventRecord
{
    private readonly DbContext database;
    private readonly EventAppender<TEvent, TStream, TStoredEvent> appender;
    private readonly string streamType;
    private readonly Dictionary<Guid, LoadedStream> loadedStreams = [];
    private readonly List<ProjectionBinding> requiredProjections = [];
    private ProjectionBinding? aggregateState;

    protected EventStore(
        DbContext database,
        EventRecordAdapter<TEvent, TStream, TStoredEvent> records,
        TimeProvider timeProvider
    )
    {
        appender = new(database, records, timeProvider);
        this.database = database;
        streamType = records.StreamType;
    }

    /// <summary>Supplies trusted ownership and extra fields only. The store assigns stream metadata.</summary>
    protected abstract TStream CreateStream(Guid id);

    /// <summary>A raw stream may reuse its existing history reader. Inline stores configure an adapter instead.</summary>
    protected virtual Task<TAggregate> LoadAggregateAsync(
        TStream stream,
        CancellationToken cancellationToken
    ) =>
        throw new InvalidOperationException("Configure aggregate state or a raw aggregate reader.");

    protected void ConfigureMainState<TRow>(InlineAggregateAdapter<TAggregate, TRow> adapter)
        where TRow : class, IInlineStateRecord
    {
        ArgumentNullException.ThrowIfNull(adapter);
        if (aggregateState is not null || loadedStreams.Count != 0)
            throw new InvalidOperationException(
                "Configure one aggregate state before using the store."
            );
        if (
            !RequiredInlineStateExtensions.IsRegistered(
                database.Model,
                typeof(TStream),
                typeof(TStoredEvent),
                typeof(TRow),
                streamType
            )
        )
            throw new InvalidOperationException(
                "Register this aggregate state in the native model/save contract."
            );
        aggregateState = new AggregateStateBinding<TRow>(database, adapter);
        requiredProjections.Add(aggregateState);
    }

    protected void ConfigureRequiredProjection<TRow>(InlineEventProjection<TEvent, TRow> projection)
        where TRow : class, IInlineStateRecord
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (aggregateState is null || loadedStreams.Count != 0)
            throw new InvalidOperationException(
                "Configure required projections after aggregate state, before use."
            );
        if (requiredProjections.Any(participant => participant.RowType == typeof(TRow)))
            throw new InvalidOperationException("An inline row type is already configured.");
        if (
            !RequiredInlineStateExtensions.IsRegistered(
                database.Model,
                typeof(TStream),
                typeof(TStoredEvent),
                typeof(TRow),
                streamType,
                isMainState: false
            )
        )
            throw new InvalidOperationException(
                "Register this required projection in the native model/save contract."
            );
        requiredProjections.Add(new SecondaryProjectionBinding<TRow>(database, projection));
    }

    public async Task<TAggregate?> GetForWritingAsync(
        Guid id,
        long? expectedVersion = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        if (expectedVersion is < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        var transaction = RequireTransaction();
        if (database.Set<TStream>().Local.Any(row => row.Id == id))
            throw new InvalidOperationException(
                "Use one terminal append per stream in a fresh context."
            );
        loadedStreams.Remove(id);
        TStream? stream = await database
            .Set<TStream>()
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (stream is null)
        {
            stream = CreateStream(id);
            ArgumentNullException.ThrowIfNull(stream);
            stream.Id = id;
            stream.StreamType = streamType;
            if (stream.Version != 0 || database.Entry(stream).State != EntityState.Detached)
                throw new InvalidOperationException("Return a detached unstarted stream.");
            if (!ReferenceEquals(transaction, database.Database.CurrentTransaction))
                throw new InvalidOperationException(
                    "The native transaction changed during loading."
                );
            loadedStreams[id] = new(stream, null, transaction, expectedVersion, []);
            return null;
        }
        ValidateHeader(stream);
        if (expectedVersion is { } expectation && expectation != stream.Version)
            throw new DbUpdateConcurrencyException(
                "The stream differs from the command's expected version."
            );
        var states = new object?[requiredProjections.Count];
        for (int index = 0; index < requiredProjections.Count; index++)
            states[index] = await requiredProjections[index].LoadAsync(stream, cancellationToken);
        TAggregate aggregate = aggregateState is null
            ? await LoadAggregateAsync(stream, cancellationToken)
            : aggregateState.Restore(states[0]!);
        if (
            aggregate.Id != id
            || aggregate.ExpectedVersion != stream.Version
            || aggregate.Version != stream.Version
            || aggregate.PendingEvents.Count != 0
        )
            throw new InvalidDataException(
                "Reconstituted aggregate does not represent the captured stream version."
            );
        if (!ReferenceEquals(transaction, database.Database.CurrentTransaction))
            throw new InvalidOperationException("The native transaction changed during loading.");
        loadedStreams[id] = new(stream, aggregate, transaction, expectedVersion, states);
        return aggregate;
    }

    public Task<EventAppendResult> AppendAsync(
        TAggregate aggregate,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        cancellationToken.ThrowIfCancellationRequested();
        if (
            !loadedStreams.TryGetValue(aggregate.Id, out var loaded)
            || loaded.Staged
            || !ReferenceEquals(loaded.Transaction, RequireTransaction())
            || (loaded.Aggregate is not null && !ReferenceEquals(loaded.Aggregate, aggregate))
        )
            throw new InvalidOperationException(
                "Append the aggregate from this store's active observation once."
            );
        if (
            loaded.Aggregate is null
            && (aggregate.ExpectedVersion != 0 || loaded.Expectation is > 0)
        )
            throw new DbUpdateConcurrencyException(
                "Creation requires an loaded missing stream at version zero."
            );
        if (aggregate.PendingEvents.Count == 0)
            throw new InvalidOperationException(
                "Append requires accepted facts; an empty decision needs no append."
            );
        var append = appender.Prepare(loaded.Stream, aggregate, cancellationToken);
        TEvent[] events = aggregate.PendingEvents.ToArray();
        var changes = new List<PreparedInlineProjection>();
        for (int index = 0; index < requiredProjections.Count; index++)
            changes.Add(
                requiredProjections[index]
                    .Prepare(
                        loaded.Stream,
                        loaded.States.Length == 0 ? null : loaded.States[index],
                        aggregate,
                        events,
                        append.NextVersion,
                        append.RecordedAt
                    )
            );
        cancellationToken.ThrowIfCancellationRequested();
        append.Stage(cancellationToken);
        foreach (var change in changes)
            change.Stage();
        loaded.Staged = true;
        return Task.FromResult(new EventAppendResult(append.NextVersion, append.RecordedAt));
    }

    private IDbContextTransaction RequireTransaction() =>
        database.Database.CurrentTransaction
        ?? throw new InvalidOperationException(
            "The store requires the caller's active native transaction."
        );

    private void ValidateHeader(TStream stream)
    {
        if (
            stream.StreamType != streamType
            || stream.Version < 1
            || stream.CreatedAt.Offset != TimeSpan.Zero
            || stream.UpdatedAt.Offset != TimeSpan.Zero
            || stream.CreatedAt > stream.UpdatedAt
        )
            throw new InvalidDataException(
                "The captured stream family/version/timestamps are invalid."
            );
    }

    private sealed class LoadedStream(
        TStream stream,
        TAggregate? aggregate,
        IDbContextTransaction transaction,
        long? expectation,
        object?[] states
    )
    {
        internal TStream Stream { get; } = stream;
        internal TAggregate? Aggregate { get; } = aggregate;
        internal IDbContextTransaction Transaction { get; } = transaction;
        internal long? Expectation { get; } = expectation;
        internal object?[] States { get; } = states;
        internal bool Staged { get; set; }
    }

    private abstract class ProjectionBinding
    {
        internal abstract Type RowType { get; }
        internal abstract Task<object> LoadAsync(TStream stream, CancellationToken token);

        internal virtual TAggregate Restore(object state) => throw new InvalidOperationException();

        internal abstract PreparedInlineProjection Prepare(
            TStream stream,
            object? committed,
            TAggregate aggregate,
            IReadOnlyList<TEvent> events,
            long version,
            DateTimeOffset time
        );
    }

    private sealed class AggregateStateBinding<TRow>(
        DbContext database,
        InlineAggregateAdapter<TAggregate, TRow> adapter
    ) : ProjectionBinding
        where TRow : class, IInlineStateRecord
    {
        private readonly InlineProjectionStorage<TStream, TRow> rows = new(database);
        internal override Type RowType => typeof(TRow);

        internal override async Task<object> LoadAsync(TStream stream, CancellationToken token) =>
            await rows.LoadAsync(stream, token);

        internal override TAggregate Restore(object state) => adapter.Restore((TRow)state);

        internal override PreparedInlineProjection Prepare(
            TStream stream,
            object? committed,
            TAggregate aggregate,
            IReadOnlyList<TEvent> events,
            long version,
            DateTimeOffset time
        ) => rows.Prepare(stream, (TRow?)committed, adapter.CreateRecord(aggregate), version, time);
    }

    private sealed class SecondaryProjectionBinding<TRow>(
        DbContext database,
        InlineEventProjection<TEvent, TRow> projection
    ) : ProjectionBinding
        where TRow : class, IInlineStateRecord
    {
        private readonly InlineProjectionStorage<TStream, TRow> rows = new(database);
        internal override Type RowType => typeof(TRow);

        internal override async Task<object> LoadAsync(TStream stream, CancellationToken token) =>
            await rows.LoadAsync(stream, token);

        internal override PreparedInlineProjection Prepare(
            TStream stream,
            object? committed,
            TAggregate aggregate,
            IReadOnlyList<TEvent> events,
            long version,
            DateTimeOffset time
        ) =>
            rows.Prepare(
                stream,
                (TRow?)committed,
                projection.Evolve((TRow?)committed, events),
                version,
                time
            );
    }
}
