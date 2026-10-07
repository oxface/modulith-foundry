# Explicit JSON event serialization

A package-free .NET 10 codec for explicitly registered durable event identities and native
JSON payloads. Adopt it without EF, HTTP, DI, actor identity, tenancy, messaging or the sample
module layout. `Events` is a naming family; there is no umbrella package or shared event base.

## Consumer usage

Define your own family interface and concrete reference event types, then construct one codec
with native `JsonSerializerOptions` and explicit registrations:

```csharp
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
};
var codec = new JsonEventCodec<IStockPositionEvent>(options,
[
    EventRegistration<IStockPositionEvent>.For<StockPositionOpened>(
        "inventory.stock-position.opened", 1),
    EventRegistration<IStockPositionEvent>.For<StockPositionReceived>(
        "inventory.stock-position.received", 1),
]);

SerializedEvent stored = codec.Serialize(received);
IStockPositionEvent decoded = codec.Deserialize(
    stored.EventName, stored.SchemaVersion, stored.Payload);
```

See the actual [Inventory](../../../samples/Wholesale/EventCodecDemo/Inventory/StockPositionExample.cs)
and [Purchasing](../../../samples/Wholesale/EventCodecDemo/Purchasing/PurchaseOrderExample.cs)
registration and [standalone consumer](../../../samples/Wholesale/EventCodecDemo/README.md).

## Guarantees and errors

Construction copies registrations into private dictionaries and clones/freezes native JSON
options. Each concrete event type has one write identity per codec; each exact name/version
pair has one type. Conflicts fail with `ArgumentException` instead of depending on order.
Names must be nonblank and versions positive. Accepted names are preserved and compared
ordinally, without normalization. Abstract/interface registrations are rejected.

Serialization dispatches the exact runtime type, including when the value is passed through
its family interface. An unregistered type raises `InvalidOperationException`; no base-type
fallback, CLR-derived alias or additional payload discriminator is supplied. Serialized output
contains its registered name/version and native `JsonElement`. `SerializedEvent` is an ordinary
data record, not a validator for envelopes manually constructed by a consumer.

Deserialization selects only an exact registered pair. Unknown names and unsupported versions
raise `EventDecodingException` with `UnknownEvent = 1`; null results, undefined payloads and
native `JsonException` failures use `InvalidPayload = 2`. The requested name/version are
available on the exception. The outer message does not include the payload; native inner
JSON exceptions are retained and can carry converter diagnostics. Consumers choose logging
and attach stream/event position or product error context. No event is skipped or upgraded.

Other converter/configuration exceptions retain their native type. Null arguments are caller
errors. The caller parses source JSON and supplies a live element during decoding; malformed
JSON text, disposed source documents and envelope-field extraction remain caller concerns.
Serialization returns a payload without a caller-owned temporary document to keep alive.

## Consumer choices and limits

Required fields, optional defaults, converters, enum representation, naming and unmapped-field
policy belong to your native options/event contract. The codec does not force the sample's
JSON policy or validate business rules. See native
[required fields/constructor parameters](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/required-properties)
and [nullable annotations and their limits](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/nullable-annotations).
Both sample families deliberately enforce their required fields in different native ways.

Changing the original options or registration collection cannot rebind an established codec.
Native converter/resolver objects are shared by reference, not deep-cloned; their mutable
state, thread safety and lifetime remain your responsibility. With concurrently usable
converters/resolvers, an established codec supports concurrent calls without rebinding its
registry/options. There is no disposal, operation scope or I/O inside the codec.

Use ordinary construction or your chosen DI lifetime. The codec provides no discovery,
registration middleware, domain-event collection, dispatch, persistence, transaction, retry,
generated application code or stream metadata. Multiple read schemas per CLR type, upcasting,
other serialization engines, AOT support and transport compatibility require separate proofs.

The typed registration factory has one local CA1000 suppression: the family type belongs on
the registration and the method selects its concrete event type. There is no global analyzer
disable or extra factory facade. Review it with [the interface plan](../../../docs/plans/e4-event-serialization.md)
and [proof report](../../../docs/reports/e4-event-serialization.md).

## Deferred direction and event-store composition

Wholesale uses this codec for event-store envelope encoding and explicit historical decoding.
It maps each exact durable name/schema pair back to the concrete registered CLR fact shape.
The provided event store does not depend on this package: direct JSON/other explicit encoders
remain possible. No domain-event bus or handler registry follows from these registrations.

Multiple historical schemas for one CLR type, deterministic upcasting, generated registration,
alternative serializers and AOT remain deferred. Any selected extension must preserve literal
old payload compatibility and establish errors for unsupported schemas/missing upgrade paths.
See the [event-store capability context](../../ModulithFoundry.EventSourcing/ModulithFoundry.EventSourcing.EntityFrameworkCore/docs/capabilities.md)
for related persisted-history/projection obligations; no extension is implemented by that plan.
