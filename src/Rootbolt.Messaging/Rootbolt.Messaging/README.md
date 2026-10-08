# Transport-independent outgoing messages

Package-free .NET 10 message envelope and `IMessagePublisher`. No EF, hosting, transport,
tenancy, actor identity or event-sourcing dependency.

```csharp
var message = OutgoingMessage.FromPayload(
    Guid.NewGuid(), "exports.render", "exports.render", 1,
    new RenderExportV1(request.Id, request.Pages), jsonOptions);
```

Consumers choose stable delivery IDs, logical RouteKey values, exact durable names/schema
versions, explicit JSON policy and optional opaque TenantKey. IDs must be nonempty, names/routes
nonblank, schema positive and payload defined/non-null. An optional tenant key must be nonblank.
Accepted strings are preserved. The constructor clones JSON; the source JsonDocument may
then be disposed. Get-only properties prevent mutation through record `with` syntax.

Typed construction returns a non-generic envelope, so payload types stop at the serialization
boundary. The caller supplies JsonSerializerOptions; converters and native type metadata remain
consumer choices. Already serialized JSON or Events.Serialization output uses the constructor.

Implement `IMessagePublisher.PublishAsync(OutgoingMessage,CancellationToken)`. Return only
after the adapter's documented acceptance condition; propagate publication errors and honor
cancellation. A successful return does not mean a receiver's business workflow completed.
Delivery may repeat with the same MessageId. Routes, credentials, transport lifetime and
confirmation policy remain explicit consumer code. No publisher is globally registered.

MessageId identifies one delivery and survives retries. Correlation groups related work;
causation identifies the immediate message/command causing new work. Neither is a replacement
for MessageId or business-operation idempotency. O1 has no required correlation/causation
properties or handler registry; consumer contracts/adapters may carry these values. Durable
inbox scope and metadata evolution are recorded in [the capability plan](../docs/capabilities.md).
