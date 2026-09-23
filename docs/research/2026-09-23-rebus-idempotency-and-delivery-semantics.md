# Rebus Idempotency and Delivery Semantics

Status: Research input for architecture review; not an implementation decision record.

As of: 2026-09-23

## Scope and versions

This note audits what Rebus handles automatically, what depends on the selected transport or optional configuration, and what the application must implement. It examines `Rebus` 8.9.4, `Rebus.RabbitMq` 10.1.1, `Rebus.AzureServiceBus` 10.7.1, and the related `Rebus.PostgreSql` 9.1.1 outbox. The evidence is official source, official package metadata, first-party broker documentation, and official project issues.

Primary version sources:

- [`Rebus` 8.9.4](https://www.nuget.org/packages/Rebus/8.9.4)
- [`Rebus.RabbitMq` 10.1.1](https://www.nuget.org/packages/Rebus.RabbitMq/10.1.1)
- [`Rebus.AzureServiceBus` 10.7.1](https://www.nuget.org/packages/Rebus.AzureServiceBus/10.7.1)
- [`Rebus.PostgreSql` 9.1.1](https://www.nuget.org/packages/Rebus.PostgreSql/9.1.1)

## Executive conclusion

Rebus handles transport message IDs, broker acknowledgement/abandonment, retry dispatch, poison-message forwarding, correlation, and saga persistence mechanics. Its optional `IdempotentSaga<TData>` meaningfully prevents the same Rebus message ID from executing the same saga handler twice and replays that handler's stored outgoing Rebus messages.

Rebus does **not** provide a general inbox for ordinary handlers, business-operation idempotency, or automatic atomicity between a module's EF Core transaction and broker sends. `IdempotentSaga<TData>` does not cover arbitrary EF writes, HTTP calls, file operations, email, or other external effects. Replayed outgoing messages still require idempotent downstream consumers.

For the planned module-owned EF Core process manager, retain the proposed application infrastructure:

- inbox receipt and process transition in one module transaction;
- stable operation IDs and semantic state checks;
- module-owned outbox and deadline rows in that transaction;
- an at-least-once outbox relay through Rebus; and
- idempotency at every externally visible effect.

Do not adopt `Rebus.PostgreSql` 9.1.1's handler outbox: the exact release currently has an open commit-before-save defect.

## Responsibility matrix

| Concern | Rebus/core transport behavior | Boundary or caveat | Application responsibility |
| --- | --- | --- | --- |
| Transport message ID | Rebus assigns a GUID when no `rbs2-msg-id` is supplied. RabbitMQ maps it to AMQP `MessageId`; Azure Service Bus maps it to native `MessageId`. | A resend with a new ID is new work. RabbitMQ synthesizes an ID from the body only for non-Rebus incoming messages that omit one. | Preserve IDs across retries; also carry a stable business operation ID. |
| Handler failure | Rebus catches the exception, rolls back transport callbacks, and NACKs/abandons the delivery for retry. | Default error tracking is process-local memory. Retry counting across replicas is not globally exact unless a native delivery count or distributed tracker is used. | Make handlers safe for redelivery; configure and test the desired global retry policy. |
| Successful delivery | Rebus publishes queued outgoing transport messages, then ACKs/completes the input. | A crash or settlement failure after effects or outgoing publication can cause redelivery. | Inbox and idempotent effects. |
| Outgoing sends in a handler | Sends are accumulated in the Rebus transaction context and executed during its commit callbacks; handler failure prevents those in-memory sends. | This is not a durable database outbox and is not atomic with an ordinary EF transaction. | Persist the module outbox with domain/process state. |
| `Saga<TData>` | Correlation, create/load/save/delete lifecycle, `Revision`, optimistic concurrency, optional conflict resolution, and optional exclusive locks. | An ordinary saga handler can still execute twice. An initiating insert conflict is not automatically merge-resolved. | Business transition idempotency and any domain/external effects. |
| `IdempotentSaga<TData>` | Optional exact-message-ID detection; skips duplicate handler invocation and re-sends previously recorded outgoing transport messages. | Only saga handlers; only identical message IDs; stored history grows for the saga lifetime; completion deletes the history. | Downstream inbox, business keys, completed-process tombstones, and non-message effect idempotency. |
| Ordinary handler duplicate | No successful-message inbox or deduplication in Rebus core. | Retry tracking counts failures; it is not successful processing deduplication. | Module-owned inbox. |
| RabbitMQ acknowledgement | Manual `BasicAck`; on NACK Rebus uses `BasicNack(requeue: true)`. Publisher confirms are enabled by default and ordinary messages are persistent. | ACK failure closes the consumer/channel, causing an unacknowledged delivery to return. RabbitMQ has no general MessageId deduplication. `Express` opts out of persistence and publisher confirms. | Keep publisher confirms enabled; do not use `Express` for business messages; make consumers idempotent. |
| Azure Service Bus acknowledgement | Peek-lock receive; Rebus calls `CompleteMessageAsync` on success and `AbandonMessageAsync` on NACK. | Lock expiry, failed completion, or restart can redeliver. Automatic lock renewal is optional and disabled when Rebus prefetching is enabled. | Keep handlers short or configure renewal; retain consumer idempotency. |
| Azure duplicate detection | Optional broker-side send deduplication based on native `MessageId` within a configured window. | It protects retrying sends, not consumer redelivery. Rebus' setting applies to its input queue and currently restricts the window to 20 seconds–1 day; Rebus-created topics are not configured by that setting. | Provision topic duplicate detection separately if desired; never treat it as an inbox. |
| Deferred messages | Azure transport uses native scheduled enqueue. RabbitMQ can use its optional delayed-message-exchange integration; core Rebus otherwise needs a configured persistent/external timeout manager. | Core's default timeout manager is disabled. A due message is still at-least-once work and is not atomic with EF unless separately integrated. | Prefer module deadline rows when the deadline is workflow state; make due commands idempotent. |
| Poison messages | Default Rebus policy is five delivery attempts, then forwarding to an `error` queue. Optional second-level `IFailed<T>` handling exists. | Default failure tracking is in memory. Azure native dead-lettering is an optional extension labelled experimental; default Rebus uses its own error queue. | Operate, inspect, alert, and intentionally retry/re-drive failures. |
| Business-operation idempotency | Not supplied. | Message ID dedup cannot detect the same logical command issued under another ID. | Unique operation keys, process uniqueness constraints, and valid-transition checks. |

## Message IDs are transport identity, not business identity

The outgoing pipeline adds a new GUID-valued Rebus message ID only when the caller has not supplied one. RabbitMQ copies that value to its native message property and restores it on receive; for a foreign RabbitMQ message with no ID it falls back to a deterministic body hash. Azure Service Bus likewise maps the Rebus ID to and from native `MessageId`.

Primary sources:

- [`AssignDefaultHeadersStep`: Rebus message-ID assignment](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Pipeline/Send/AssignDefaultHeadersStep.cs)
- [`RabbitMqTransport`: message-ID conversion and fallback](https://github.com/rebus-org/Rebus.RabbitMq/blob/10.1.1/Rebus.RabbitMq/RabbitMq/RabbitMqTransport.cs)
- [`DefaultMessageConverter`: Azure Service Bus message-ID mapping](https://github.com/rebus-org/Rebus.AzureServiceBus/blob/10.7.1/Rebus.AzureServiceBus/AzureServiceBus/Messages/DefaultMessageConverter.cs)

Rebus requires an incoming message ID because its retry tracker keys failures by that ID. This does not turn the ID into a durable inbox record. A later duplicate after a successful ACK, or the same logical command sent under a new message ID, is not suppressed for an ordinary handler.

Primary source: [`DefaultRetryStep`: ID requirement and failure tracking](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Retry/Simple/DefaultRetryStep.cs)

Recommendation: use both an immutable transport `MessageId` and a stable domain `OperationId`. The latter identifies the intended business effect across republishing, manual retries, and message transformations.

## Retry, commit, and acknowledgement ordering

Rebus' abstract transport queues sends in memory and registers them as commit callbacks. Its transaction context invokes commit callbacks first and ACK callbacks afterwards. A handler exception instead results in rollback callbacks and NACK callbacks. This means ordinary outgoing sends made by a failing handler are not published; on success, publication is attempted before the incoming delivery is acknowledged.

Primary sources:

- [`AbstractRebusTransport`: in-memory outgoing queue and commit callback](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Transport/AbstractRebusTransport.cs)
- [`TransactionContext`: commit followed by ACK, rollback followed by NACK](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Transport/TransactionContext.cs)
- [`DefaultRetryStep`: success, failure, retry, and error handling](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Retry/Simple/DefaultRetryStep.cs)

This ordering is useful but is not a distributed transaction. Two important ambiguous outcomes remain:

1. Module database work may commit, then the process can fail before an outgoing broker send succeeds.
2. An outgoing send may succeed, then input acknowledgement can fail or the process can stop, causing input redelivery and potentially another outgoing send.

The default retry policy is five attempts, an error queue named `error`, no second-level retries, and an in-memory `IErrorTracker`. The tracker documentation explicitly describes process-local cleanup; Rebus 8.9.4 also understands an optional transport-supplied delivery-count header. Therefore multiple competing instances cannot rely on the default tracker alone for a globally exact delivery cap.

Primary sources:

- [`RetryStrategySettings`: defaults](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Retry/Simple/RetryStrategySettings.cs)
- [`RebusConfigurer`: default `InMemErrorTracker`](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Config/RebusConfigurer.cs)
- [`InMemErrorTracker`: process-local implementation](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Retry/ErrorTracking/InMemErrorTracker.cs)

## RabbitMQ transport behavior

`Rebus.RabbitMq` consumes with automatic acknowledgement disabled. It registers `BasicAck` on the Rebus ACK callback and `BasicNack(requeue: true)` on the NACK callback. If acknowledgement cannot be completed, it disposes the consumer, leaving the delivery unacknowledged so RabbitMQ can requeue it. RabbitMQ's own reliability documentation requires consumers to tolerate such redeliveries.

The transport enables publisher confirms by default, uses a separate unconfirmed channel only for messages marked `Express`, retries an outgoing send operation up to three times, and marks non-express messages persistent. Publisher confirms establish that RabbitMQ accepted responsibility for the publish; they do not coordinate with the consumer acknowledgement or the module database transaction.

Primary sources:

- [`RabbitMqTransport`: send, ACK/NACK, confirms, and persistence](https://github.com/rebus-org/Rebus.RabbitMq/blob/10.1.1/Rebus.RabbitMq/RabbitMq/RabbitMqTransport.cs)
- [`RabbitMqOptionsBuilder`: publisher confirms enabled by default](https://github.com/rebus-org/Rebus.RabbitMq/blob/10.1.1/Rebus.RabbitMq/Config/RabbitMqOptionsBuilder.cs)
- [RabbitMQ acknowledgements, redelivery, and publisher confirms](https://www.rabbitmq.com/docs/confirms)

RabbitMQ does not perform general deduplication by Rebus message ID. Publisher reconnection after an ambiguous confirmation can produce duplicates. The application inbox remains required.

## Azure Service Bus transport behavior

`Rebus.AzureServiceBus` receives under peek lock. Rebus calls `CompleteMessageAsync` on ACK and `AbandonMessageAsync` on NACK. A failed completion, expired lock, detached link, or process restart can cause redelivery. The transport tracks each delivery by lock token because the same message ID may legitimately be in flight more than once if a lock expires and the broker redelivers it.

Automatic peek-lock renewal is optional. Rebus disables its own renewal when prefetching is enabled, assuming prefetched messages will be handled quickly.

Primary sources:

- [`AzureServiceBusTransport`: receive, lock renewal, complete, and abandon](https://github.com/rebus-org/Rebus.AzureServiceBus/blob/10.7.1/Rebus.AzureServiceBus/AzureServiceBus/AzureServiceBusTransport.cs)
- [`AzureServiceBusTransportSettings`: prefetch and renewal configuration](https://github.com/rebus-org/Rebus.AzureServiceBus/blob/10.7.1/Rebus.AzureServiceBus/Config/AzureServiceBusTransportSettings.cs)
- [Azure Service Bus transfers, locks, and settlement](https://learn.microsoft.com/en-us/azure/service-bus-messaging/message-transfers-locks-settlement)

Azure Service Bus duplicate detection is optional send-side protection: the broker accepts but discards a repeated native `MessageId` within its configured window. Microsoft explicitly says this does not replace idempotent receive-side processing. Rebus can configure duplicate detection on the endpoint's input queue, but its current API restricts that window to 20 seconds through one day, while Azure currently permits up to seven days. Rebus' automatic topic creation does not apply the input-queue duplicate-detection setting to topics.

Primary sources:

- [`AzureServiceBusTransportSettings.SetDuplicateDetectionHistoryTimeWindow`](https://github.com/rebus-org/Rebus.AzureServiceBus/blob/10.7.1/Rebus.AzureServiceBus/Config/AzureServiceBusTransportSettings.cs)
- [`AzureServiceBusTransport`: input-queue and topic creation](https://github.com/rebus-org/Rebus.AzureServiceBus/blob/10.7.1/Rebus.AzureServiceBus/AzureServiceBus/AzureServiceBusTransport.cs)
- [Azure Service Bus duplicate detection](https://learn.microsoft.com/en-us/azure/service-bus-messaging/duplicate-detection)
- [Microsoft guidance: duplicate detection does not replace idempotent consumers](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-message-loss-and-duplicates)

Rebus creates its Azure input queue with a broker `MaxDeliveryCount` of 100 and normally applies Rebus' own retry/error-queue behavior. `UseNativeMessageDeliveryCount()` can expose the broker count to the Rebus retry step. `UseNativeDeadlettering()` is optional, and its source labels it experimental. The baseline should use one explicitly tested strategy rather than assume the Rebus error queue and Azure DLQ compose automatically.

Primary sources:

- [`AzureServiceBusTransport`: queue creation and native delivery count](https://github.com/rebus-org/Rebus.AzureServiceBus/blob/10.7.1/Rebus.AzureServiceBus/AzureServiceBus/AzureServiceBusTransport.cs)
- [`UseNativeDeadlettering`: experimental native DLQ adapter](https://github.com/rebus-org/Rebus.AzureServiceBus/blob/10.7.1/Rebus.AzureServiceBus/Config/AdditionalAzureServiceBusConfigurationExtensions.cs)

## `Saga<TData>` and concurrency

Rebus' saga pipeline:

1. inspects configured message-to-data correlations;
2. loads matching saga data, or creates data for an initiating message;
3. dispatches the handler;
4. inserts, updates, or deletes data after successful handling; and
5. uses `ISagaData.Revision` and the persistence provider's concurrency contract to reject conflicting writes.

The saga author may override conflict resolution for update conflicts. Rebus does not attempt conflict resolution for competing inserts. Optional exclusive-access middleware can use process-local semaphore buckets or an application-supplied distributed lock, but that is not enabled automatically.

Primary sources:

- [`Saga<TData>`: correlation, completion, and conflict-resolution hook](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Sagas/Saga.cs)
- [`LoadSagaDataStep`: lifecycle and concurrency retry](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Sagas/LoadSagaDataStep.cs)
- [`ISagaStorage`: concurrency contract](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Sagas/ISagaStorage.cs)
- [`ExclusiveAccessConfigurationExtensions`: optional locking](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Sagas/Exclusive/ExclusiveAccessConfigurationExtensions.cs)

Correlation is not idempotency. A duplicate message can find the same ordinary saga and invoke it again. Conversely, a non-initiating late message for a missing/completed saga is skipped by the default correlation error handler. The product may need retained tombstones, explicit stale-message audit, or reconciliation rather than silent skipping.

Primary source: [`DefaultCorrelationErrorHandler`: default skip behavior](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Config/DefaultCorrelationErrorHandler.cs)

## What `IdempotentSaga<TData>` actually adds

This is a real but opt-in Rebus feature. The saga data must implement `IIdempotentSagaData`, the saga must derive from `IdempotentSaga<TData>`, and the bus must call `EnableIdempotentSagas()`.

On first handling, Rebus records the incoming Rebus message ID in saga data and captures every outgoing serialized Rebus transport message plus its destination addresses. If that same incoming ID is delivered again, Rebus skips saga-handler invocation and re-sends the stored outgoing messages. Because saga data is saved after the handler and before Rebus commits transport sends, this repairs the narrow failure window where saga state was saved but its output send or input ACK had an ambiguous result.

Primary sources:

- [`EnableIdempotentSagas`: opt-in pipeline wiring and intended guarantee](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Sagas/Idempotent/IdempotentSagaConfigurationExtensions.cs)
- [`IdempotentSagaIncomingStep`: duplicate skip and output replay](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Sagas/Idempotent/IdempotentSagaIncomingStep.cs)
- [`IdempotentSagaOutgoingStep`: capture of outgoing transport messages](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Sagas/Idempotent/IdempotentSagaOutgoingStep.cs)
- [`IdempotencyData`: handled IDs and stored output](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Sagas/Idempotent/IdempotencyData.cs)

Its limits are material:

- it applies only to the saga instance, not normal Rebus handlers;
- only the exact incoming Rebus message ID is recognized;
- it captures Rebus outgoing messages, not EF writes or non-message effects;
- replay intentionally re-sends outputs, so downstream consumers must deduplicate them;
- handled IDs and serialized outgoing messages have no pruning API and grow with the saga; and
- `MarkAsComplete()` deletes saga data and therefore its deduplication history. A late duplicate initiating message can then create a new saga. This last point is a direct inference from the completion/deletion and initiation paths.

The feature is worth a spike, but it does not replace the proposed process-manager inbox/outbox model.

## Inbox and business idempotency remain application work

Rebus core has no durable record saying that an ordinary handler successfully processed `(consumer, message ID)`. Its error tracker records failures, not successful completion. Therefore an ordinary EF process-manager adapter still needs a module-owned inbox with a unique key such as:

```text
(consumer_name, message_id)
```

The inbox row, process transition, owned domain changes, deadline changes, audit entries, and resulting module outbox rows must commit together.

That transport inbox is still insufficient for logical retries with new message IDs. Each important business command also needs an operation key and state-specific semantics, for example:

```text
(tenant_id, reservation_id, operation_id)
```

`ReleaseReservation` can then return success for the same completed operation, reject a conflicting operation for a consumed reservation, and audit a tenant/process mismatch. Rebus cannot infer those rules.

## PostgreSQL outbox: current blocker

`Rebus.PostgreSql` 9.1.1 includes an experimental outbox decorator. Outside a handler, `UseOutbox(NpgsqlConnection, NpgsqlTransaction)` can store outgoing messages using a caller-provided physical transaction. An application could theoretically attach an EF Core `DbContext` to that same connection and transaction, but there is no direct EF integration and it still requires a focused proof.

Inside a Rebus handler, version 9.1.1 opens its own PostgreSQL transaction and registers that transaction's commit callback before the callback that saves outgoing messages. The project's open issue 55 reports that this commits the database transaction before the outbox insert; the issue points to the exact source commit used by the 9.1.1 tag and reports the defect becoming visible with .NET 10/current Npgsql.

Primary sources:

- [`UseOutbox`: caller-provided PostgreSQL connection and transaction](https://github.com/rebus-org/Rebus.PostgreSql/blob/9.1.1/Rebus.PostgreSql/Config/Outbox/OutboxExtensions.cs)
- [`OutboxIncomingStep`: handler transaction commit registration](https://github.com/rebus-org/Rebus.PostgreSql/blob/9.1.1/Rebus.PostgreSql/PostgreSql/Outbox/OutboxIncomingStep.cs)
- [`OutboxClientTransportDecorator`: later outbox-save registration](https://github.com/rebus-org/Rebus.PostgreSql/blob/9.1.1/Rebus.PostgreSql/PostgreSql/Outbox/OutboxClientTransportDecorator.cs)
- [Open `Rebus.PostgreSql` issue 55: commit before saving messages](https://github.com/rebus-org/Rebus.PostgreSql/issues/55)

The package's outbox table also has no unique processed-message/inbox record; its schema is a queue of outgoing messages. Its forwarder deletes claimed rows transactionally, publishes them, and then commits deletion. A publish followed by a failed delete commit can legitimately send the same outbox message again, so consumer idempotency remains required.

Primary sources:

- [`PostgreSqlOutboxStorage`: schema, save, claim, and delete](https://github.com/rebus-org/Rebus.PostgreSql/blob/9.1.1/Rebus.PostgreSql/PostgreSql/Outbox/PostgreSqlOutboxStorage.cs)
- [`OutboxForwarder`: publish then complete claimed batch](https://github.com/rebus-org/Rebus.PostgreSql/blob/9.1.1/Rebus.PostgreSql/PostgreSql/Outbox/OutboxForwarder.cs)

Recommendation: build the small module-owned EF inbox/outbox needed by the example and keep Rebus behind the publishing/receiving adapter. Do not use the current `Rebus.PostgreSql` handler outbox.

## Deferred messages and workflow deadlines

Core Rebus registers a disabled timeout manager unless a transport or application configures another implementation. Its persistent timeout flow stores a deferred transport message, polls due messages, sends each through the configured transport, and only then marks it completed. That is naturally at-least-once under an ambiguous send/completion failure.

Azure Service Bus integration replaces that pipeline with native scheduled enqueue by mapping the Rebus deferred timestamp to `ScheduledEnqueueTime`. RabbitMQ can use Rebus' optional delayed-message-exchange adapter, but that requires the RabbitMQ delayed-message-exchange plugin; otherwise a durable `ITimeoutManager` or external timeout manager is required.

Primary sources:

- [`RebusConfigurer`: disabled timeout-manager default](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Config/RebusConfigurer.cs)
- [`HandleDeferredMessagesStep`: persistent/external timeout flow](https://github.com/rebus-org/Rebus/blob/8.9.4/Rebus/Pipeline/Receive/HandleDeferredMessagesStep.cs)
- [`Rebus.AzureServiceBus` message converter: native scheduling](https://github.com/rebus-org/Rebus.AzureServiceBus/blob/10.7.1/Rebus.AzureServiceBus/AzureServiceBus/Messages/DefaultMessageConverter.cs)
- [`Rebus.RabbitMq` delayed-exchange adapter](https://github.com/rebus-org/Rebus.RabbitMq/blob/10.1.1/Rebus.RabbitMq/Config/RabbitMqDelayedMessageExchangeExtensions.cs)

For business workflow deadlines, the cleaner baseline remains a deadline row owned by the process manager and committed with its state. A native `BackgroundService` claims due rows and writes stable-ID commands to the module outbox. Transport-native scheduling can later be used as an optimization, not the sole record of the business deadline.

## Required reliability proofs

Before approving the messaging implementation, the example should prove at least these cases against real RabbitMQ and, in a smaller compatibility suite, real Azure Service Bus Standard:

1. Crash after module commit but before publish: the outbox relay eventually publishes once or more with the same ID.
2. Publish succeeds but relay state update fails: duplicate publication produces one observable consumer effect.
3. Consumer commits but broker ACK/complete fails: redelivery is suppressed by the module inbox.
4. The same logical command arrives under a new message ID: the stable operation ID prevents a second business effect.
5. Two replicas receive competing transitions: one state transition wins and the other becomes a valid no-op, stale result, or retry.
6. A poison message reaches the selected error mechanism after the intended global attempt limit.
7. A process deadline is claimed by competing workers and causes one observable transition.
8. A completed process receives a late duplicate or response and records an intentional stale-message outcome rather than silently recreating work.
9. An idempotent Rebus saga spike demonstrates its exact-message skip/output replay and measures saga-state growth before any decision to adopt it.

## Decision impact

The earlier five-layer idempotency model remains necessary. Rebus substantially supplies transport retries and broker acknowledgements, and optionally supplies exact-message protection for Rebus saga state/output. It does not eliminate:

1. module inbox deduplication;
2. unique process initiation;
3. valid and repeatable transition semantics;
4. stable business-operation IDs; or
5. idempotency/reconciliation for external effects.

The proposed baseline—normal Rebus transport adapters invoking a module-owned EF process manager—is still the cleanest transaction boundary. `IdempotentSaga<TData>` should be tested as a narrowly scoped alternative, not treated as a replacement for the application's reliability model.
