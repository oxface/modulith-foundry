# Events composition and capabilities

Current scope, 2026-10-07. This family guide and the package-local contracts travel with
the source. Root plans/reports retain cross-cutting review and dated execution evidence.

## Supported composition

Serialization writes the exact registered runtime fact type and reads only the exact durable
name/schema pair. Registration collisions fail; unknown identities and invalid payloads have typed
decoding errors. History validates a materialized, already ordered (afterVersion, throughVersion]
range for contiguous positions, exact bounds and nondecreasing recorded time. It neither selects
rows nor certifies excluded history.

Select only the packages needed by the consumer:

- [ModulithFoundry.Events.Serialization](../ModulithFoundry.Events.Serialization/README.md)
- [ModulithFoundry.Events.History](../ModulithFoundry.Events.History/README.md)

## Consumer obligations

Consumers define event families and schemas, native JSON options, explicit registrations, database
queries and pure domain reconstruction. Wholesale composes both utilities; neither is required by
the EventSourcing store or the independent direct-JSON counter. No shared domain-event base,
umbrella package, bus or handler registry is introduced.

The package READMEs specify public types, explicit integration steps, errors and unsupported
configuration. Copying or referencing one package does not automatically install another
family's context or policy. Consumers keep DI lifetimes, host wiring and native dependency
selection visible.

## Deferred context

Upcasting, several historical schemas for one CLR type, generated discovery, AOT, other serializers,
generic history loading, snapshots and safe global feeds are deferred. Compatibility needs literal
historical payload tests; persisted progress and concurrency claims need real database proofs. The
EventSourcing capability catalog records related projection/stream directions.

Deferred features need executable contract proofs before support can be claimed. No future
package or interface is implemented by this repository relocation.

[Related event-store capability catalog](../../ModulithFoundry.EventSourcing/ModulithFoundry.EventSourcing.EntityFrameworkCore/docs/capabilities.md) records the persisted-stream and projection directions.
