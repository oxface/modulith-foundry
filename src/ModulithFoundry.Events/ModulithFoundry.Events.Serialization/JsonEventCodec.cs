using System.Text.Json;

namespace ModulithFoundry.Events.Serialization;

/// <summary>Dispatches explicit durable event identities through consumer-configured native JSON.</summary>
public sealed class JsonEventCodec<TEvent>
    where TEvent : class
{
    private readonly Dictionary<Type, EventRegistration<TEvent>> byType = [];
    private readonly Dictionary<(string Name, int Version), Type> byIdentity = [];
    private readonly JsonSerializerOptions options;

    public JsonEventCodec(
        JsonSerializerOptions options,
        IEnumerable<EventRegistration<TEvent>> registrations
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(registrations);
        foreach (EventRegistration<TEvent> registration in registrations)
        {
            ArgumentNullException.ThrowIfNull(registration);
            if (!byType.TryAdd(registration.EventType, registration))
                throw new ArgumentException(
                    "An event CLR type has more than one write registration.",
                    nameof(registrations)
                );
            if (
                !byIdentity.TryAdd(
                    (registration.EventName, registration.SchemaVersion),
                    registration.EventType
                )
            )
                throw new ArgumentException(
                    "A durable event name/version has more than one registration.",
                    nameof(registrations)
                );
        }
        this.options = new JsonSerializerOptions(options);
        this.options.MakeReadOnly(populateMissingResolver: true);
    }

    public SerializedEvent Serialize(TEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        Type type = @event.GetType();
        if (!byType.TryGetValue(type, out var registration))
            throw new InvalidOperationException("The event runtime type is not registered.");
        return new SerializedEvent(
            registration.EventName,
            registration.SchemaVersion,
            JsonSerializer.SerializeToElement(@event, type, options)
        );
    }

    public TEvent Deserialize(string eventName, int schemaVersion, JsonElement payload)
    {
        ArgumentNullException.ThrowIfNull(eventName);
        if (!byIdentity.TryGetValue((eventName, schemaVersion), out Type? type))
            throw new EventDecodingException(
                eventName,
                schemaVersion,
                EventDecodingFailure.UnknownEvent
            );
        if (payload.ValueKind == JsonValueKind.Undefined)
            throw new EventDecodingException(
                eventName,
                schemaVersion,
                EventDecodingFailure.InvalidPayload
            );
        try
        {
            return (TEvent?)payload.Deserialize(type, options)
                ?? throw new EventDecodingException(
                    eventName,
                    schemaVersion,
                    EventDecodingFailure.InvalidPayload
                );
        }
        catch (JsonException exception)
        {
            throw new EventDecodingException(
                eventName,
                schemaVersion,
                EventDecodingFailure.InvalidPayload,
                exception
            );
        }
    }
}
