using System.Text.Json;

namespace Rootbolt.Messaging;

/// <summary>A retained delivery identity and consumer-selected wire contract, independent of transport.</summary>
public sealed class OutgoingMessage
{
    /// <summary>Creates an immutable wire envelope and clones its JSON payload.</summary>
    /// <param name="messageId">Delivery identity retained unchanged across publication retries.</param>
    /// <param name="routeKey">Logical routing key interpreted by the consumer's publisher.</param>
    /// <param name="messageName">Stable wire-contract alias, independent of CLR type names and routing.</param>
    /// <param name="schemaVersion">Positive version of the named wire contract.</param>
    /// <param name="payload">Already serialized, non-null JSON; serialization policy belongs to the consumer.</param>
    /// <param name="tenantKey">Optional opaque tenant metadata. This neither admits a tenant nor grants authority.</param>
    public OutgoingMessage(
        Guid messageId,
        string routeKey,
        string messageName,
        int schemaVersion,
        JsonElement payload,
        string? tenantKey = null
    )
    {
        ArgumentOutOfRangeException.ThrowIfEqual(messageId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(schemaVersion);
        if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            throw new ArgumentException(
                "A message requires a non-null JSON payload.",
                nameof(payload)
            );
        if (tenantKey is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);

        MessageId = messageId;
        RouteKey = routeKey;
        MessageName = messageName;
        SchemaVersion = schemaVersion;
        // Own the payload independently of the caller's JsonDocument lifetime.
        Payload = payload.Clone();
        TenantKey = tenantKey;
    }

    /// <summary>Serializes a typed payload using explicit consumer options and returns a non-generic envelope.</summary>
    /// <typeparam name="TPayload">The producer's wire payload type; it does not propagate into storage or dispatch.</typeparam>
    /// <param name="messageId">Delivery identity retained unchanged across publication retries.</param>
    /// <param name="routeKey">Logical routing key interpreted by the consumer's publisher.</param>
    /// <param name="messageName">Stable wire-contract alias, independent of CLR type names.</param>
    /// <param name="schemaVersion">Positive version of the named wire contract.</param>
    /// <param name="payload">The non-null wire payload to serialize.</param>
    /// <param name="serializerOptions">Consumer-selected JSON policy, including converters and type metadata.</param>
    /// <param name="tenantKey">Optional opaque tenant metadata; grants no authority.</param>
    public static OutgoingMessage FromPayload<TPayload>(
        Guid messageId,
        string routeKey,
        string messageName,
        int schemaVersion,
        TPayload payload,
        JsonSerializerOptions serializerOptions,
        string? tenantKey = null
    )
    {
        ArgumentNullException.ThrowIfNull(serializerOptions);
        return new(
            messageId,
            routeKey,
            messageName,
            schemaVersion,
            JsonSerializer.SerializeToElement(payload, serializerOptions),
            tenantKey
        );
    }

    /// <summary>Identifies this delivery; all retries retain the same value.</summary>
    public Guid MessageId { get; }

    /// <summary>Consumer-defined logical routing key, not necessarily a physical queue or endpoint.</summary>
    public string RouteKey { get; }

    /// <summary>Stable wire-contract alias; together with SchemaVersion identifies the payload contract.</summary>
    public string MessageName { get; }

    /// <summary>Positive version of the named wire contract.</summary>
    public int SchemaVersion { get; }

    /// <summary>Owned JSON value, independent of the source JsonDocument's lifetime.</summary>
    public JsonElement Payload { get; }

    /// <summary>Optional opaque tenant metadata. Does not admit a tenant or grant access.</summary>
    public string? TenantKey { get; }
}
