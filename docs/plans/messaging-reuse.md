# Messaging reuse after the Sales round trip

This is an extraction comparison, not approval to add a framework or change delivery guarantees. Review any proposed library as a separate, self-contained increment after the round trip's evidence is accepted.

## Repeated mechanics

Inventory and Sales each own concrete `Messaging/Persistence` inbox/outbox types and a `Messaging/*OutboxRelay`. Both store an incoming message identity, tenant and payload fingerprint; stage JSONB payloads in the business transaction; claim committed outgoing work with PostgreSQL `FOR UPDATE SKIP LOCKED`, a token and a 30-second lease; use publisher confirms; and mark or back off a dispatch only while holding its token. Their native hosted workers and generated logs have the same operational shape.

Both composition classes create isolated Rebus service providers, forward only host infrastructure, use stable logical message aliases, textual wire enums, fresh scope delivery and module-local error queues. Both transport adapters check header/payload identity and producer before invoking a Rebus-free application handler.

## Differences that must stay explicit

- Inventory publishes an outcome topic; Sales sends a directed Inventory command. Routing/subscription and payload serialization are module composition concerns, not interchangeable bus calls.
- Inventory atomically commits an event stream, inline model, reservation operation, inbox, audit and outcome outbox. Sales commits a versioned process/owned lines, inbox, curated activity and audit. A shared helper must not own these business transactions or trigger implicit SaveChanges/event cascades.
- Inventory persists a separate reservation-operation tombstone; Sales retains terminal per-line outcomes on its process. Semantic identity, fingerprint fields, correlation and replay behavior belong to those use cases, not a generic hash of arbitrary DTOs.
- Sales additionally recovers pending processes and resolves MAIN through an Inventory contract. This policy, deadlines and compensation/replenishment transitions are not relay infrastructure.
- Invitation-email delivery protects a recoverable secret and calls a mail transport; it is not automatically the same outbox as integration messaging.

## Candidate extraction boundary

Consider a small PostgreSQL/EF reliability library for inbox/outbox row models, explicit EF registration and lease/dispatch mechanics, with a separate Rebus adapter if useful. The owning module must retain schema, transaction, handler, message mapping, trust/correlation and business idempotency. Contracts must remain free of EF and Rebus. There is no generic saga, repository, mediator or workflow DSL justified here.

Before choosing the public API, account for the still-open cancellation/confirmation bound, multi-replica retry/lease races, retention/tombstones and operational redrive. Keep behavior-focused Contracts/broker tests through extraction. Apply the deletion test: if the library disappears, meaningful infrastructure duplication should return; a wrapper that merely renames a Rebus method is not a useful library.

## Purchasing bootstrap evidence

The third endpoint repeats isolated provider/transport registration, stable aliases, producer and delivery checks, inbox receipt and local transaction semantics. Its full-state reference receiver has no outgoing business event yet. Snapshot capture, committed-prefix feed ordering, a subscription-before-export barrier, coalesced buffering, checkpoint installation and per-item revision semantics are **new protocol responsibilities**, not automatically reusable inbox helpers. Keep their concrete proof and recovery limits visible until a second bootstrap consumer earns extraction. No library is added with 5.3a.
