namespace ModulithFoundry.EventSourcing.EntityFrameworkCore;

/// <summary>Configured persistence encoding and consumer row fields for one explicit stream family.</summary>
public abstract class EventRecordAdapter<TEvent, TStream, TStoredEvent>
    where TEvent : class
    where TStream : class, IEventStreamRecord
    where TStoredEvent : class, IStoredEventRecord
{
    public abstract string StreamType { get; }

    /// <summary>Returns a fresh detached envelope with encoding/ownership fields only; do not retain or mutate it.</summary>
    public abstract TStoredEvent CreateRecord(TEvent fact, TStream stream);
}
