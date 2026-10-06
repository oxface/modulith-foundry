namespace ModulithFoundry.EventSourcing.EntityFrameworkCore;

/// <summary>Consumer-populated stream header fields, without ownership or domain state.</summary>
public interface IEventStreamRecord
{
    Guid Id { get; set; }
    string StreamType { get; set; }
    long Version { get; set; }
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}
