using Microsoft.EntityFrameworkCore;
using ModulithFoundry.EventSourcing.EntityFrameworkCore;

namespace ModulithFoundry.Samples.EventStorageDemo;

internal sealed class CounterStore
    : EventStore<CounterAggregate, CounterEvent, EventStreamRecord, StoredEventRecord>
{
    private const string StreamType = "proof.counter";
    private readonly StorageDbContext database;

    internal CounterStore(StorageDbContext database, TimeProvider timeProvider)
        : base(database, new CounterEventRecordAdapter(), timeProvider) => this.database = database;

    protected override EventStreamRecord CreateStream(Guid id) =>
        new() { Description = "Append counter" };

    protected override async Task<CounterAggregate> LoadAggregateAsync(
        EventStreamRecord stream,
        CancellationToken cancellationToken
    )
    {
        var state = await LoadStateAsync(stream, cancellationToken);
        return CounterAggregate.FromState(stream.Id, stream.Version, state.Value);
    }

    internal async Task<CounterState?> ReadAsync(Guid id, CancellationToken cancellationToken) =>
        (await LoadAsync(id, cancellationToken)).State;

    private async Task<(EventStreamRecord? Stream, CounterState? State)> LoadAsync(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        var stream = await database
            .Streams.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (stream is null)
            return (null, null);
        return (stream, await LoadStateAsync(stream, cancellationToken));
    }

    private async Task<CounterState> LoadStateAsync(
        EventStreamRecord stream,
        CancellationToken cancellationToken
    )
    {
        Guid id = stream.Id;
        if (
            stream.StreamType != StreamType
            || stream.Version < 1
            || stream.CreatedAt > stream.UpdatedAt
        )
            throw new InvalidDataException("The counter header is invalid.");
        // Capture once, then load only the ordered prefix through that observed head.
        var rows = await database
            .Events.AsNoTracking()
            .Where(row => row.StreamId == id && row.StreamVersion <= stream.Version)
            .OrderBy(row => row.StreamVersion)
            .ToArrayAsync(cancellationToken);
        if (
            rows.LongLength != stream.Version
            || rows[0].RecordedAt != stream.CreatedAt
            || rows[^1].RecordedAt != stream.UpdatedAt
        )
            throw new InvalidDataException("The counter prefix differs from the captured header.");
        var facts = new CounterEvent[rows.Length];
        for (int index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (
                row.StreamVersion != index + 1L
                || row.SchemaVersion != 1
                || row.RecordedAt.Offset != TimeSpan.Zero
                || (index > 0 && row.RecordedAt < rows[index - 1].RecordedAt)
            )
                throw new InvalidDataException("The counter prefix metadata is invalid.");
            facts[index] = row.EventName switch
            {
                "proof.counter-started" when index == 0 => new CounterStarted(
                    row.Payload.GetProperty("value").GetInt32()
                ),
                "proof.counter-increased" when index > 0 => new CounterIncreased(
                    row.Payload.GetProperty("amount").GetInt32()
                ),
                _ => throw new InvalidDataException("The counter fact sequence is invalid."),
            };
        }
        // Historical facts do not re-run today's ceiling policy.
        return new CounterState(
            id,
            stream.Version,
            CounterEvolution.Evolve(null, facts).Value,
            stream.UpdatedAt
        );
    }
}
