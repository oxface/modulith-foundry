# Durable message observability sample and proofs

Status: proposed for owner review, 2026-10-09. This extends the monitoring discussion
around [E9](e9-explicit-transactional-audit.md); it is not an implemented guarantee.
E9 is complete at checkpoint `18a76f8`, present in `origin/main`. No runtime changes accompany
this proposal. The [remaining roadmap](remaining-capability-roadmap.md) identifies it as OBS1,
alongside W1's separate worker-host proof; reuse that topology rather than create a second
competing host-composition abstraction.

## Actual code and native support

The HTTP host calls the editable Wholesale ServiceDefaults. Its native OTel setup subscribes
to ASP.NET Core, HttpClient and Npgsql tracing, host logs and request/runtime/database metrics.
OTLP export is enabled when an endpoint is configured. Existing TelemetryTests exercise
native in-memory exporters for request/database spans, failure logs and metrics. The AppHost
currently orchestrates PostgreSQL, the HTTP API, explicit demo setup and optional Keycloak.

EventPersistenceDemo is a finite console application without ServiceDefaults or an exporter.
The prior draft's local ActivityListener demonstrated an audit TraceId, not distributed
export; both were removed when the owner simplified audit under YAGNI. ServiceDefaults does not subscribe to that journey's source or RabbitMQ sources.
IncomingMessage, OutgoingMessage and their durable rows retain business correlation and
causation, but no W3C trace context. Fresh workers cannot recover the earlier producer span.

The pinned RabbitMQ.Client 7.2.1 already provides Publisher/Subscriber ActivitySources,
native context injection/extraction and linked receive spans. Enabling those sources supplies
transport instrumentation; durable queue processing still needs explicit retained context.
Its publisher injects the current send activity, so simply prepopulating trace headers does
not establish a stable message creation context across retries.

## One bounded outcome

Run the existing stock-issue command, accepted-change audit and durable reply with real
PostgreSQL/RabbitMQ and separate producer/worker processes. Export their telemetry to the
Aspire dashboard through host-owned OTLP configuration. Demonstrate delayed processing,
one retry and a restart using the same executable path. Retain the separate existing human
Sales profile business audit demonstration without tracing fields; it is not a producer for the unrelated Inventory command.

Use explicit setup for migrations and stock fixtures. Long-running workers must not seed,
migrate or clear tables on startup. Publish confirms, database commit and broker acknowledgement
remain visible native operations. This establishes a production-oriented local topology and
fault proofs, not a deployed production system or a Purchasing/Sales process manager.

## Proposed public interface delta

Append optional `string? traceParent = null, string? traceState = null` parameters to
IncomingMessage and OutgoingMessage constructors and OutgoingMessage.FromPayload. Add matching
read-only `string? TraceParent` and `string? TraceState` properties. Retain them in nullable
`trace_parent` / `trace_state` columns in the existing native inbox/outbox mappings.

These are explicitly supplied diagnostic metadata. The library does not read Activity.Current,
infer an actor/tenant, initialize a workflow correlation ID or require an OTel SDK/exporter.
The adapter supplies valid W3C values; absent/malformed received diagnostic context is ignored
without rejecting a valid business delivery. Transport size limits remain adapter-owned.
Existing callers and historical rows work without tracing. Native System.Diagnostics supplies
context parsing, ActivitySource and ActivityLink; avoid a new tracing abstraction package.

Outgoing pending-envelope validation must include captured context, since it is part of the
immutable staged proposal. Inbox duplicate comparison must continue to compare delivery
identity and business envelope, **excluding diagnostic context**: publication retries can
carry different send spans. Keep the first committed intake's context. Changed payload,
contract, tenant, correlation or cause remains a conflicting delivery under the existing rules.
New additive sample migrations upgrade populated databases; merged migrations stay unchanged.

## Concrete usage and span relationships

1. A producer starts a native creation activity, captures its W3C ID/state when available,
   supplies these to the outgoing envelope and commits the existing native transaction.
   The activity can end before dispatch. The durable row retains its creation context.
2. A fresh outbox dispatch attempt links to that retained creation context. Its RabbitMQ
   publication uses the client's native span and header propagation. A subsequent attempt
   gets its own span while retaining the same message ID, conversation, cause and payload.
3. The receiver retains the received W3C context with durable intake before acknowledgement.
   A fresh inbox process activity links to the retained context and covers handler execution,
   SaveChanges, completion and commit. Failures record an error, not a committed success.
4. Accepted processing stages its business audit and reply in the owning transaction. Audit
   records no correlation, causation or trace IDs. The reply retains its messaging conversation
   and immediate cause, captures processing context when staged, then crosses its own outbox
   publication boundary.

Use links across asynchronous boundaries rather than force every retry and delayed job into
one trace. OTel messaging conventions recommend links by default; a single-message parent is
an allowed alternative. The sample chooses one documented policy and verifies actual links.
The existing native callable inbox/outbox paths are the proposed instrumentation locations,
using the source name `Rootbolt.Messaging`; no new public dispatcher or processor interface
is needed. Source subscriptions and exporters belong in editable host ServiceDefaults.

## Monitoring experience

Register Rootbolt.Messaging plus RabbitMQ.Client.Publisher and RabbitMQ.Client.Subscriber
sources in the participating hosts. Give producer and worker distinct service names and
instances. Show how to follow producer/consumer span relationships and inspect message/conversation
IDs, failures and retries separately from the business audit timeline. Span/log attributes carry opaque message/correlation/
causation IDs; business payloads, names and addresses are not telemetry attributes.

Add sample-owned metrics for processing/publication outcomes and duration, pending work and
oldest pending age. Use bounded labels such as operation/subscription/result; never individual
message, item, tenant, actor or trace IDs as metric dimensions. Sample-owned native read queries
observe inbox/outbox backlog. Polling intervals and thresholds remain consumer configuration.
Document the dashboard views for backlog, repeated failures and last successful processing;
process liveness alone does not prove that queued work is advancing.

## Required executable proofs

- Native exporter assertions for span context/links, successful commit and errors, correlated
  logs and metric values. Assert structural relationships rather than merely nonempty IDs.
- Real broker/database round trip in separate processes, with the producer exited before
  dispatch and the intake process exited before processing. No AsyncLocal state can bridge it.
- Restart and duplicate/redelivery: one accepted mutation/audit/reply; stable business IDs;
  changed retry trace context accepted as the same delivery; first retained intake context wins.
- Broker publication failure and inbox processing failure: retry/error telemetry, retained
  pending work and observable recovery, with existing transaction/lease guarantees preserved.
- Tracing absent, unrecorded sampling context and malformed incoming tracing metadata:
  correct business processing/audit independent of whether a span is exported.
- Populated pre-change migration upgrade and nullable historical diagnostic metadata.
- Aspire OTLP observation using actual exported spans/logs/metrics, separately reported from
  automated in-memory exporter tests. Exporter unavailability must not decide business commit;
  measure shutdown before making a bounded-shutdown guarantee.

No new tests were run for this proposal and no new reusable mechanism was proven. Retained
diagnostic envelope fields and native callable-path instrumentation are extraction candidates.
Topology, adapters, monitoring queries, dashboard instructions and alert thresholds remain
editable sample/template concerns. Authorization, attribution, audit classification and
retention remain consumer-owned. Template promotion follows exercised setup, not this plan.

## Primary evidence

- [Pinned RabbitMQ activity sources and native propagator](https://github.com/rabbitmq/rabbitmq-dotnet-client/blob/v7.2.1/projects/RabbitMQ.Client/Impl/RabbitMQActivitySource.cs).
- [Pinned RabbitMQ publishing/header injection](https://github.com/rabbitmq/rabbitmq-dotnet-client/blob/v7.2.1/projects/RabbitMQ.Client/Impl/Channel.BasicPublish.cs).
- [OTel messaging context and span relationships](https://opentelemetry.io/docs/specs/semconv/messaging/messaging-spans/)
  (messaging conventions are currently marked Development; document the adopted conventions).
- [.NET native distributed tracing and HTTP propagation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-concepts).
- [Editable Aspire ServiceDefaults and host observability wiring](https://aspire.dev/get-started/csharp-service-defaults/).
