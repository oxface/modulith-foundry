using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModulithFoundry.Modules.Purchasing.PurchaseOrders.Events;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

internal static class PurchaseOrderEventSerializer
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters =
        {
            new JsonStringEnumConverter<PurchaseOrderStatus>(
                JsonNamingPolicy.CamelCase,
                allowIntegerValues: false
            ),
        },
    };
    private static readonly Dictionary<Type, StoredEventTypeAttribute> ByType = Build();
    private static readonly Dictionary<(string Name, int Version), Type> ByIdentity =
        ByType.ToDictionary(x => (x.Value.Name, x.Value.SchemaVersion), x => x.Key);

    internal static void ValidateRegistry() => _ = ByIdentity.Count;

    private static Dictionary<Type, StoredEventTypeAttribute> Build()
    {
        var result = new Dictionary<Type, StoredEventTypeAttribute>();
        var identities = new HashSet<(string, int)>();
        foreach (
            var type in typeof(IPurchaseOrderEvent)
                .Assembly.GetTypes()
                .Where(x => !x.IsAbstract && typeof(IPurchaseOrderEvent).IsAssignableFrom(x))
        )
        {
            var identity =
                type.GetCustomAttribute<StoredEventTypeAttribute>()
                ?? throw new InvalidOperationException(
                    $"Event {type.Name} has no durable identity."
                );
            if (
                string.IsNullOrWhiteSpace(identity.Name)
                || identity.SchemaVersion < 1
                || !identities.Add((identity.Name, identity.SchemaVersion))
            )
                throw new InvalidOperationException(
                    "Invalid or duplicate Purchase Order event identity."
                );
            result.Add(type, identity);
        }
        return result;
    }

    internal static SerializedPurchaseOrderEvent Serialize(IPurchaseOrderEvent @event)
    {
        var identity = ByType[@event.GetType()];
        return new(
            identity.Name,
            identity.SchemaVersion,
            JsonSerializer.SerializeToElement(@event, @event.GetType(), Options)
        );
    }

    internal static IPurchaseOrderEvent Deserialize(StoredEvent stored)
    {
        if (!ByIdentity.TryGetValue((stored.EventName, stored.SchemaVersion), out var type))
            throw new PurchaseOrderIntegrityException(
                stored.StreamId,
                PurchaseOrderIntegrityFailure.UnknownEvent
            );
        try
        {
            return (IPurchaseOrderEvent)(
                stored.Payload.Deserialize(type, Options) ?? throw new JsonException()
            );
        }
        catch (JsonException exception)
        {
            throw new PurchaseOrderIntegrityException(
                stored.StreamId,
                PurchaseOrderIntegrityFailure.InvalidEventPayload,
                exception
            );
        }
    }
}

internal sealed record SerializedPurchaseOrderEvent(string Name, int Version, JsonElement Payload);
