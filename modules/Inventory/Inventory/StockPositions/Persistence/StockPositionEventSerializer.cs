using System.Reflection;
using System.Text.Json;
using ModulithFoundry.Modules.Inventory.StockPositions.Events;

namespace ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

internal static class StockPositionEventSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly Dictionary<Type, StoredEventTypeAttribute> ByType = BuildRegistry(
        typeof(IStockPositionEvent).Assembly.GetTypes());
    private static readonly Dictionary<(string Name, int Version), Type> ByIdentity =
        ByType.ToDictionary(entry => (entry.Value.Name, entry.Value.SchemaVersion), entry => entry.Key);

    internal static void ValidateRegistry() => _ = ByIdentity.Count;

    internal static SerializedStockPositionEvent Serialize(IStockPositionEvent @event)
    {
        if (!ByType.TryGetValue(@event.GetType(), out StoredEventTypeAttribute? identity))
        {
            throw new InvalidOperationException("Stock Position event type is not registered.");
        }

        return new SerializedStockPositionEvent(
            identity.Name,
            identity.SchemaVersion,
            JsonSerializer.SerializeToElement(@event, @event.GetType(), SerializerOptions));
    }

    internal static IStockPositionEvent Deserialize(StoredEvent stored)
    {
        if (!ByIdentity.TryGetValue((stored.EventName, stored.SchemaVersion), out Type? type))
        {
            throw new InvalidOperationException(
                $"Unknown Stock Position event '{stored.EventName}' schema version {stored.SchemaVersion}.");
        }

        return (IStockPositionEvent)(stored.Payload.Deserialize(type, SerializerOptions)
            ?? throw new InvalidOperationException("Stored Stock Position event payload was empty."));
    }

    internal static Dictionary<Type, StoredEventTypeAttribute> BuildRegistry(IEnumerable<Type> types)
    {
        var registry = new Dictionary<Type, StoredEventTypeAttribute>();
        var identities = new HashSet<(string Name, int Version)>();
        foreach (Type type in types
                     .Where(type => !type.IsAbstract && typeof(IStockPositionEvent).IsAssignableFrom(type)))
        {
            StoredEventTypeAttribute identity = type.GetCustomAttribute<StoredEventTypeAttribute>()
                ?? throw new InvalidOperationException($"Event '{type.Name}' has no durable identity.");
            if (string.IsNullOrWhiteSpace(identity.Name) || identity.SchemaVersion < 1
                || !identities.Add((identity.Name, identity.SchemaVersion)))
            {
                throw new InvalidOperationException($"Event '{type.Name}' has an invalid or duplicate durable identity.");
            }

            registry.Add(type, identity);
        }

        return registry;
    }
}

internal sealed record SerializedStockPositionEvent(
    string EventName,
    int SchemaVersion,
    JsonElement Payload);
