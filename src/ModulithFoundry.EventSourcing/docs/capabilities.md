# EventSourcing composition and capabilities

Current scope, 2026-10-07. This family guide and the package-local contracts travel with
the source. Root plans/reports retain cross-cutting review and dated execution evidence.

## Supported composition

The aggregate core carries captured/proposed versions and ordered heterogeneous facts. The EF store
requires the consumer's explicit transaction, loads state at a captured stream version, validates
family/version/root association and stages the accepted event batch plus registered required inline
state. Explicit native save validation enforces tracked event/header/main-state/required-view
participation. Registered aggregate writes require one main inline state; raw history-backed streams
remain permitted.

Select only the packages needed by the consumer:

- [ModulithFoundry.EventSourcing](../ModulithFoundry.EventSourcing/README.md)
- [ModulithFoundry.EventSourcing.EntityFrameworkCore](../ModulithFoundry.EventSourcing.EntityFrameworkCore/README.md)

## Consumer obligations

Consumers own domain eligibility, event schemas/encoding, reducers, native query filters, tenant
admission/ownership, typed module DbContexts, migrations and final SaveChanges/commit. One stored
envelope shape supports different payload types; optional StoredEventRecord uses JsonElement, with
JSONB configured in the PostgreSQL consumer. Additional inline views are projections of the stream
rather than command aggregates. Events.Serialization/History remain optional utilities.

The package READMEs specify public types, explicit integration steps, errors and unsupported
configuration. Copying or referencing one package does not automatically install another
family's context or policy. Consumers keep DI lifetimes, host wiring and native dependency
selection visible.

## Deferred context

No catch-up/rebuild engine, async or multi-stream projections, snapshot-plus-tail, upcasting,
messaging, audit participation, cross-module transaction contract, automatic retry or
ambiguous-commit recovery is supplied. PostgreSQL is the only proven provider; other providers are
not deliberately rejected or certified. Pessimistic locks and a possible EventSourcing.Postgres
adapter remain candidates requiring a concrete portable seam and provider proofs.

Deferred features need executable contract proofs before support can be claimed. No future
package or interface is implemented by this repository relocation.

The detailed local records retain the full supported contract and future proof obligations:

- [Aggregate core capabilities](../ModulithFoundry.EventSourcing/docs/capabilities.md)
- [EF store capabilities and deferred catalog](../ModulithFoundry.EventSourcing.EntityFrameworkCore/docs/capabilities.md)

The Events family remains separate: [Serialization](../../ModulithFoundry.Events/ModulithFoundry.Events.Serialization/README.md) and [History](../../ModulithFoundry.Events/ModulithFoundry.Events.History/README.md) are used by Wholesale, while the independent counter needs neither. There is no Postgres project until a concrete provider capability earns its own reviewed interface.
