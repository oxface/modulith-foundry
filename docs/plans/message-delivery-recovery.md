# Retained Inventory outcome recovery

Increment 5.6b1 proves one explicit operational recovery: Inventory has committed a Reservation Release, but Sales cannot record its outcome because a temporary database fault exhausts its local technical retries. Inventory stock is already correct; Sales remains compensation-pending. Repairing Sales alone does not make the retained outcome arrive again.

## Capability and trust

[`IInventoryMessageDeliveryRecovery`](../../modules/Inventory/Inventory.Contracts/MessageDelivery/IInventoryMessageDeliveryRecovery.cs) is an Inventory-owned, authorized operator Contract. `GetAsync` returns one tenant-scoped delivery's identity/type, creation time, next attempt, publication mark, lease expiry and dispatch attempts. It exposes no payload, lease token, credentials or broker handle. Publication metadata is not proof that Sales consumed the outcome.

`RequeueAsync` supports retained reservation and release outcomes only. It requires a verified current Organization context, current `inventory.message-deliveries.recover` permission (included in Inventory Manager), observed dispatch-attempt count and a bounded printable operator reason. Other message types are refused; this is not an arbitrary replay surface. Organization Administrator alone has no business permission. Authorization is point-in-time, like existing Inventory administrative operations; no commit-time revocation lock is claimed.

There is no public HTTP route, operator UI or CLI in this increment. A future host-owned maintenance adapter must establish the normal verified context and invoke this Contract; it must not treat caller-supplied user/tenant IDs as proof of access. The tested Contract is a usable composition seam, not a shipped standalone operator tool.

## Recovery transaction

The operation locks the retained outbox row in its own schema, then refreshes it so EF's previously tracked state cannot hide a relay advance. It compares the observed attempt count and refuses an unexpired publication lease. Lease checks and scheduling use PostgreSQL `clock_timestamp()`, the same clock as the relay. PostgreSQL row locks exclude concurrent updates until the transaction ends, while `clock_timestamp()` supplies wall-clock time rather than the transaction-start timestamp. See [row locking](https://www.postgresql.org/docs/current/explicit-locking.html#LOCKING-ROWS) and [date/time functions](https://www.postgresql.org/docs/current/functions-datetime.html#FUNCTIONS-DATETIME-CURRENT). The clock is read through native [EF scalar SQL queries](https://learn.microsoft.com/en-us/ef/core/querying/sql-queries#querying-scalar-non-entity-types).

Requeue clears the publication mark/expired lease and makes the retained message immediately available. It preserves MessageId, type, stored payload, creation time, semantic-operation identity and dispatch-attempt count. The existing relay owns the next attempt, publication and token-fenced dispatch mark. Requeue and an operator audit (actor, message, prior schedule/publication, attempt count and reason) commit atomically. An already available unleased message returns `AlreadyQueued` without another update/audit; a later relay claim advances the count and makes an old operator request stale.

The row lock serializes competing operators with each other and with relay database updates; it does not cancel a broker publication. An expired lease can belong to a slow live publisher, so duplicate publication remains possible and consumer idempotency is required. No new event append, projection evolution, reservation decision or workflow completion is performed by recovery.

## Operator procedure

1. Identify the stuck process/line and owning module. An expired response deadline is a diagnostic, never evidence that Inventory did not commit.
2. Inspect the named destination error queue without consuming/deleting its records. For this scenario, Sales uses `modulith-foundry.sales.error`. Capture the outcome's MessageId and tenant/process correlation privately; do not paste full payloads into telemetry or tickets. The tests consume this queue through a test-only observer; that observer is not the production procedure.
3. Diagnose and repair the receiver fault before redrive. If the original failed delivery is retained, explicit destination-queue redrive with original body/identity is one option. It is distinct from source republication and must preserve transport trust/correlation headers and the destination's retry behavior.
4. For source republication, inspect the retained Inventory delivery through `GetAsync` under the verified Inventory Manager context. Confirm its type and correlation from controlled evidence; retain the observed attempt count. `NotFound` does not justify creating an invented outcome or new business operation.
5. Call `RequeueAsync` with the original MessageId, observed count and repair reason. `InFlight` means do not steal a live lease. `VersionConflict` means inspect again. `AlreadyQueued` means wait for the existing relay. None of these responses proves subscriber completion.
6. Verify both sides through their Contracts: Inventory current/history should show the same completed release and stream version; Sales fulfilment/activity should reach compensated with one completion. Then check the delivery publication mark/attempt count. A fresh technical failure requires diagnosis, not an automated loop of operator requeue calls.
7. Preserve error-queue evidence until receiver recovery is verified, then apply an explicit retention/redrive policy. Do not delete inbox receipts, semantic tombstones or event history to make retries pass. Dispose the failed DI scope after an unexpected fault and retry through a fresh scope.

Replaying the original command is **not** source republication: an Inventory delivery receipt suppresses handling, and a new MessageId for the same business operation records only a new receipt. Neither path republishes an already retained outcome. Do not change the OperationId, MessageId, payload or cancellation reason merely to evade these guards.

## Evidence and extraction findings

[`InventoryDeliveryRecoveryTests`](../../tests/BrokerTests/InventoryDeliveryRecoveryTests.cs) uses the existing PostgreSQL/RabbitMQ Broker lane and real Access/Inventory/Sales composition. A Sales audit fault drives a real release outcome into Sales's error queue. After repair, authorized Inventory republication completes the real Sales process with the original outcome identity and no additional stock/history effect. Tests also cover stale EF scope refresh, actor/context/input denial, lease states, competing operators and transaction participant failure. SQL only arranges faults/leases/constraints; product assertions use Contracts, including an audit-once test constraint that exposes failed rollback or repeated pending audit without querying audit rows.

The tested distinction is command redrive versus retained publication recovery. Scheduling/lease mechanics may be reusable during extraction, but the supported message set, permission, semantic identity, audit and receiving workflow interpretation remain owning-module choices. No generic replay framework, automatic compensation inference or event-source recovery API was introduced.

## Remaining boundaries

- This capability finds one delivery by known MessageId, not a generic payload browser, backlog dashboard or global operations search. It does not provide relay depth/age, duplicate counters, error-queue metrics or process-age telemetry yet.
- Purchasing/Sales publisher recovery, the production maintenance adapter, broker inspection/redrive tooling and retention remain subsequent operational work. Never claim source republication alone repairs an absent subscription, incompatible schema, permanent poison or deleted retained payload.
- Exactly-once publication, globally bounded cross-replica retries, cancellation-ingress death, broker/database outages and slow-publisher lease races retain their separate 5.6 proof requirements.
- Original identity requires compatible retained message schemas and consumers. Changing a CLR type/serializer must not silently change the meaning or fingerprint of a replayed integration message; compatibility fixtures still belong to the later contract/extraction gate.
