# Broker durability proof boundaries

This evidence ledger supplements the module protocols; it is not an exactly-once delivery claim or a reusable operations framework. Keep it aligned with the actual tests as Increment 5.6 closes its remaining failure windows.

## 5.6a — Competing receiver replicas

[ReceiverReplicaTests](../../tests/BrokerTests/ReceiverReplicaTests.cs) uses the existing test-only `BrokerReceiver` process and real module composition with retained PostgreSQL/RabbitMQ Testcontainers. Both processes consume the same production module input queue. The in-process fixture endpoint is stopped before the children own that queue.

For each case:

1. The first process commits a real incoming delivery, then a test-only pipeline step pauses before ACK.
2. A second process starts while the first is still alive. A bounded burst repeats a semantic operation under a new MessageId, with repeated delivery of that MessageId. The burst exceeds the paused endpoint's current bounded prefetch; the test requires an explicit handled signal from the second process rather than assuming fair distribution or sleeping.
3. Contracts confirm the second process did not repeat the business effect.
4. The test terminates the first process abruptly. The second process handles the original unacknowledged MessageId after broker redelivery. Contracts confirm retained state and history/activity.

Process signals coordinate these windows; they are not the asserted product state. No SQL table assertions, transport replacement, reduced production prefetch, new queue naming, fake module or production fault hook is introduced.

| Seam | Asserted business outcome |
| --- | --- |
| Inventory reserve command, outcome topic, current/history Contracts | Four units remain reserved; stream version is 3 and history contains one reservation. |
| Inventory release command, outcome topic, current/history Contracts | Reserved quantity remains zero; stream version is 4 and history contains one release. |
| Sales release-outcome topic and fulfilment/activity Contracts | Compensation stays completed at the retained process version, with one release activity and one completion activity. |
| Purchasing create-requirement command, created topic and request/requirement Contracts | The original four-unit requirement identity, number, snapshot and creation time remain; the operation stays completed and the list contains one requirement. |

All four cases run in the existing Broker CI lane, without personal credentials or a new harness. They were added one at a time against the existing implementation; passing baseline behavior required no production change. They cover an already committed operation, not simultaneous first-time uncommitted appends/inserts.

## Template/library findings

- Retained delivery receipts and semantic-operation identities protect different boundaries. A second endpoint instance cannot rely on the first process's memory; the tests exercise existing shared PostgreSQL state while both processes are alive.
- Settlement loss can cause processing again after a successful local commit. Tests require one business effect, not one invocation or one broker delivery.
- The existing isolated endpoint composition and process harness suffice for this proof. No new reusable mechanism or library boundary was introduced.
- Explicit transaction participants, semantic fingerprints and process transitions remain module-owned. Inbox/outbox leasing and transport adapter mechanics remain extraction candidates, not newly approved generic reliability APIs.

## Remaining 5.6 work

5.6b2a adds [module-owned messaging operational signals](messaging-observability.md): sampled committed outbox depth/age/expired leases with freshness, observation/relay/dispatch failure counters and matching inbox-receipt suppression counters. Its export follow-up proves all three module meter/backlog names reach the API's configured OTLP receiver during two fresh application lifecycles. Native numeric behavior remains a separate proof; production telemetry retention, process/projection, broker error-queue and full failure-matrix evidence remain open. Diagnostics do not automatically replay work or infer subscriber completion.

5.6b1 now supplies a narrow [Inventory retained-outcome recovery](message-delivery-recovery.md) proof: a real Sales receiver failure leaves compensation pending after Inventory's release, and explicit authorized republication of the original outcome closes that gap without a second stock effect. This is not automatic process reconciliation or a production operator UI/CLI. The runbook separates receiver redrive from source republication; generic metrics and the broader operational matrix remain open.

This proof does not close graceful shutdown, two complete application replicas and their shared workers, first-time concurrent receipt insertion, module failure isolation under outage, broker/database interruption, stalled outbox lease recovery across replicas, cancellation-ingress process death, globally bounded poison retries, or reconciliation after a missing response. Nor does it prove every message type on each endpoint.

5.6b adds narrowly justified diagnostics and explicit operator recovery. 5.6c closes the remaining failure matrix against the actual application. An expired business response deadline is diagnostic, not evidence of failure or permission to fabricate a new operation identity. Redrive retains original intent/identity and requires the owning protocol's idempotency proof. Until those increments land, do not present automatic retry exhaustion handling or operator reconciliation as implemented.
