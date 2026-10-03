# Library and template design decisions

Status: direction approved by the owner on 2026-10-03. Public interfaces, package divisions,
and implementation increments in [the extraction plan](plans/library-extraction.md) remain
review proposals.

## Purpose and ownership

Build useful .NET libraries, a consumer-owned repository template, and a working sample.
Use the archived wholesale sample to identify guarantees and failure cases; reimplement
mechanisms when that gives a clearer design. Domain completeness is not the goal.

The template demonstrates one application composing business modules with explicit
Contracts, internal implementations, and separately owned persistence. Architecture checks
and policy middleware are reusable, opt-in tools with consumer-selected policies; the
library does not prescribe every consumer's project graph or module structure.

Authentication establishes identity. Tenant/actor execution context is independent of the
sample Access model and does not grant authorization. Membership, invitation lifecycle,
roles, permissions, and business authorization belong to the consumer.

## Explicit control

Consumers register their DbContexts, entity mappings, migrations, handlers, transports,
queues, topics, subscriptions, and routes. Libraries may provide explicit registration
utilities and opt-in middleware whose effects and requirements are documented.

Use native EF transaction control where it suffices. A custom persistence scope must earn
its place by establishing a necessary protocol; document its entry, save, commit, disposal,
nesting, cancellation, and failure semantics. Saves and commits remain explicit. Middleware
does not silently commit, retry, compensate, or dispatch domain events.

Domain events are collected and processed deliberately. Integration contracts and their
mapping remain consumer-owned. Replay produces no historical external effects.

Supply callable delivery/maintenance operations separately from a simple optional hosted
worker. Consumers may host those operations themselves. Competing-consumer mechanisms need
their own claim, ordering, concurrency, and recovery evidence; leader election is excluded.

Library execution uses ordinary C# calls. Runtime code generation, a mediator, generic
repositories, and a generic workflow DSL are excluded. Existing archived EF migrations are
retained as evidence; new migration/scaffolding tooling choices must be stated in their slice.

## Dependencies and configuration

Start with explicit EF Core/PostgreSQL persistence and optional Rebus/RabbitMQ integration.
Critter Stack is a behavioral reference for messaging and persistence, not a selected runtime
dependency. Keep provider-specific capabilities available; a provider-neutral promise needs
evidence from actual alternatives rather than a lowest-common-denominator interface.

Library segments are independently adoptable except for documented dependencies. Event
sourcing does not require messaging; messaging does not require event sourcing; state-stored
modules do not depend on event-sourcing types or registrations. Technical libraries do not
reference sample module Contracts. Use project references during local development.

Use options for supported variations, and explicit registration or dependencies for larger
policy changes. Document how a setting changes a guarantee. Schema names, transport routes,
worker hosting, and application policy remain consumer choices.

Keep the email replacement seam narrow; SMTP/MailKit does not become a mandatory foundational
dependency. Authentication, general notifications, and a transport/provider matrix are not
automatically promoted into libraries.

## Telemetry and tooling

Libraries use native `ILogger`, `ActivitySource`, and `Meter`. The host's ServiceDefaults
configures collection, export, and health. Aspire is local orchestration and topology-test
infrastructure, not a required library runtime.

Retain `.editorconfig`, CSharpier, Lefthook, and commitlint. Introduce a pinned pnpm workspace
and shared frontend subpackages with the first exercised frontend setup; existing npm-based
repository tooling remains until that deliberate transition. CI remains authoritative.

## Evidence and supported limits

Each library increment supplies tests through its public interface and real sample usage.
Transfer the intent of archived proofs, with independent expected outcomes, rather than
equating matching implementations with correctness. The same sample domain permits useful
comparisons without freezing its old implementation.

Shared database transactions, separate commits with compensation, and durable sagas are
different guarantees. Cross-module shared transactions still require a dedicated proof;
in-process orchestration alone provides no recovery after process death.

Historical identity, repair, replay-cost, retry, and shutdown limits remain visible until
new evidence resolves them. Production provider compatibility, packaging, deployment, and
recovery are separate proof gates, not consequences of local extraction.
