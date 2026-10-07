namespace ModulithFoundry.EventSourcing.EntityFrameworkCore;

/// <summary>Consumer-owned state decoding and candidate encoding, without stream orchestration.</summary>
public abstract class InlineAggregateAdapter<TAggregate, TRow>
    where TAggregate : class
    where TRow : class, IInlineStateRecord
{
    public abstract TAggregate Restore(TRow state);

    /// <summary>Returns a fresh detached row containing candidate state; the store sets keys/version/time.</summary>
    public abstract TRow CreateRecord(TAggregate aggregate);
}

/// <summary>Consumer evolution of a required view from its own committed state.</summary>
public abstract class InlineEventProjection<TEvent, TRow>
    where TEvent : class
    where TRow : class, IInlineStateRecord
{
    /// <summary>Returns a fresh detached candidate. Do not mutate committed state or run external effects.</summary>
    public abstract TRow Evolve(TRow? committed, IReadOnlyList<TEvent> events);
}

/// <summary>Technical metadata of one inline row per stream; state shape remains consumer-owned.</summary>
public interface IInlineStateRecord
{
    Guid StreamId { get; set; }
    long Version { get; set; }
    DateTimeOffset RecordedAt { get; set; }
}
