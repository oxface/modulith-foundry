using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace ModulithFoundry.EventSourcing.EntityFrameworkCore;

/// <summary>A prepared, single-use batch bound to its original context and native transaction.</summary>
public sealed class PreparedEventAppend<TStream, TStoredEvent>
    where TStream : class, IEventStreamRecord
    where TStoredEvent : class, IStoredEventRecord
{
    private readonly DbContext _database;
    private readonly TStream _stream;
    private readonly IDbContextTransaction _transaction;
    private readonly IEventSourcedAggregate<object> _aggregate;
    private readonly object[] _pending;
    private readonly Guid _id;
    private readonly string _streamType;
    private readonly DateTimeOffset _createdAt;
    private readonly DateTimeOffset _updatedAt;
    private readonly IKey _streamKey;
    private readonly IKey _eventKey;
    private readonly IForeignKey _reference;
    private readonly object?[] _keyValues;
    private readonly List<TStoredEvent> _rows = [];
    private readonly HashSet<Guid> _identities = [];
    private bool _staged;

    internal PreparedEventAppend(
        DbContext database,
        TStream stream,
        IEventSourcedAggregate<object> aggregate,
        object[] pending,
        long nextVersion,
        DateTimeOffset recordedAt
    )
    {
        _database = database;
        _stream = stream;
        _aggregate = aggregate;
        _pending = pending;
        long expectedVersion = aggregate.ExpectedVersion;
        _transaction =
            database.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Append requires the caller's active native transaction."
            );
        if (recordedAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("Supply a UTC recorded timestamp.", nameof(recordedAt));
        if (
            stream.Id == Guid.Empty
            || string.IsNullOrWhiteSpace(stream.StreamType)
            || stream.StreamType.Length > 100
        )
            throw new ArgumentException(
                "Supply a nonempty stream identity and type of at most 100 characters.",
                nameof(stream)
            );
        if (stream.Version != expectedVersion)
            throw new DbUpdateConcurrencyException(
                "The observed header differs from the expected version."
            );
        if (expectedVersion > 0)
        {
            if (
                stream.CreatedAt.Offset != TimeSpan.Zero
                || stream.UpdatedAt.Offset != TimeSpan.Zero
                || stream.CreatedAt > stream.UpdatedAt
            )
                throw new InvalidDataException("The observed stream timestamps are invalid.");
            if (recordedAt < stream.UpdatedAt)
                throw new ArgumentOutOfRangeException(
                    nameof(recordedAt),
                    "Recorded time cannot regress."
                );
        }
        ExpectedVersion = expectedVersion;
        NextVersion = nextVersion;
        RecordedAt = recordedAt;
        _id = stream.Id;
        _streamType = stream.StreamType;
        _createdAt = stream.CreatedAt;
        _updatedAt = stream.UpdatedAt;

        var header = database.Model.FindEntityType(typeof(TStream));
        var envelope = database.Model.FindEntityType(typeof(TStoredEvent));
        if (
            header is null
            || envelope is null
            || header.FindProperty(nameof(IEventStreamRecord.Version))
                is not { IsConcurrencyToken: true, ValueGenerated: ValueGenerated.Never }
        )
            throw new InvalidOperationException(
                "Use the registered event-storage model and native header version token."
            );
        _streamKey =
            header.FindPrimaryKey()
            ?? throw new InvalidOperationException("A stream primary key is required.");
        _eventKey =
            envelope.FindPrimaryKey()
            ?? throw new InvalidOperationException("An event primary key is required.");
        _reference =
            envelope.GetForeignKeys().SingleOrDefault(key => key.PrincipalKey == _streamKey)
            ?? throw new InvalidOperationException(
                "An event reference to the stream primary key is required."
            );
        RequireKey(_streamKey.Properties, nameof(IEventStreamRecord.Id));
        RequireKey(_eventKey.Properties, nameof(IStoredEventRecord.EventId));
        RequireKey(_reference.Properties, nameof(IStoredEventRecord.StreamId));
        if (
            _reference.Properties.Count != _eventKey.Properties.Count
            || !_reference.Properties.SkipLast(1).SequenceEqual(_eventKey.Properties.SkipLast(1))
        )
            throw new InvalidOperationException(
                "Event identity and reference ownership prefixes must match."
            );
        _keyValues = Values(_streamKey.Properties, stream);
        if (_keyValues.Any(value => value is null))
            throw new ArgumentException(
                "Supply every component of the stream primary key.",
                nameof(stream)
            );
        RequireUntrackedHeader();
    }

    /// <summary>The version observed by the consumer and preserved as EF's original version.</summary>
    public long ExpectedVersion { get; }

    /// <summary>The proposed head after this complete batch, for explicit required-view preparation.</summary>
    public long NextVersion { get; }

    /// <summary>The batch's one recorded time, also used for explicit required-view preparation.</summary>
    public DateTimeOffset RecordedAt { get; }

    internal void PrepareRow(TStoredEvent row, int index)
    {
        // Check freshness before filling library-owned fields on this adapter-created row.
        if (
            _database
                .ChangeTracker.Entries<TStoredEvent>()
                .Any(entry => ReferenceEquals(entry.Entity, row))
        )
            throw new InvalidOperationException(
                "The record adapter must return fresh detached rows."
            );
        do row.EventId = Guid.NewGuid();
        while (!_identities.Add(row.EventId));
        row.StreamId = _id;
        row.StreamVersion = checked(ExpectedVersion + index + 1);
        row.RecordedAt = RecordedAt;
        row.Payload = row.Payload.Clone();
        if (!_keyValues.SequenceEqual(Values(_reference.Properties, row)))
            throw new ArgumentException(
                "The envelope reference must match the observed stream's complete key.",
                nameof(row)
            );
        RequireUntrackedEvent(row);
        _rows.Add(row);
    }

    /// <summary>
    /// Stages the header and envelopes only. Never saves, commits or stages views. Dispose the
    /// context and proposal after faults or rollback; manual tracker clearing is unsupported.
    /// </summary>
    public void Stage(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_staged || !ReferenceEquals(_transaction, _database.Database.CurrentTransaction))
            throw new InvalidOperationException(
                "Stage once within the original active native transaction."
            );
        RequireUnchanged();
        RequireUntrackedHeader();
        foreach (TStoredEvent row in _rows)
            RequireUntrackedEvent(row);
        cancellationToken.ThrowIfCancellationRequested();

        if (ExpectedVersion == 0)
        {
            _stream.Version = NextVersion;
            _stream.CreatedAt = RecordedAt;
            _stream.UpdatedAt = RecordedAt;
            _database.Set<TStream>().Add(_stream);
        }
        else
        {
            _database.Set<TStream>().Attach(_stream);
            _stream.Version = NextVersion;
            _stream.UpdatedAt = RecordedAt;
        }
        _database.Set<TStoredEvent>().AddRange(_rows);
        _staged = true;
    }

    internal void RequireUnchanged()
    {
        if (
            _aggregate.Id != _id
            || _aggregate.ExpectedVersion != ExpectedVersion
            || _aggregate.Version != NextVersion
            || !_aggregate.PendingEvents.SequenceEqual(_pending, ReferenceEqualityComparer.Instance)
        )
            throw new InvalidOperationException("The aggregate changed after preparation.");
        if (
            _stream.Id != _id
            || _stream.StreamType != _streamType
            || _stream.Version != ExpectedVersion
            || !_stream.CreatedAt.EqualsExact(_createdAt)
            || !_stream.UpdatedAt.EqualsExact(_updatedAt)
            || !_keyValues.SequenceEqual(Values(_streamKey.Properties, _stream))
        )
            throw new InvalidOperationException("The observed header changed after preparation.");
    }

    private void RequireUntrackedHeader()
    {
        if (
            _database
                .ChangeTracker.Entries<TStream>()
                .Any(entry => _keyValues.SequenceEqual(Values(_streamKey.Properties, entry.Entity)))
        )
            throw new InvalidOperationException(
                "Use one append batch per stream key in a fresh operation context."
            );
    }

    private void RequireUntrackedEvent(TStoredEvent row)
    {
        object?[] identity = Values(_eventKey.Properties, row);
        if (
            _database
                .ChangeTracker.Entries<TStoredEvent>()
                .Any(entry => identity.SequenceEqual(Values(_eventKey.Properties, entry.Entity)))
        )
            throw new InvalidOperationException("A prepared event identity is already tracked.");
    }

    private static object?[] Values(IReadOnlyList<IProperty> properties, object row) =>
        properties.Select(property => property.PropertyInfo!.GetValue(row)).ToArray();

    private static void RequireKey(IReadOnlyList<IProperty> properties, string last)
    {
        if (
            properties[^1].Name != last
            || properties.Any(property =>
                property.PropertyInfo is null
                || property.GetValueConverter() is not null
                || property.GetProviderClrType() is not null
            )
        )
            throw new InvalidOperationException(
                $"Use mapped, unconverted CLR keys ending in {last}."
            );
    }
}
