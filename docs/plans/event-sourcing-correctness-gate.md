# Event-sourcing correctness gate — 8.1b

Status: review proposal. This gate distinguishes tested guarantees from supported limits; it does not authorize library extraction or deployment. No new identity store, snapshot/checkpoint, shadow job, narrower repair lock or generic event-sourcing runtime is introduced.

## Evidence and extraction boundary

| Concern | Evidence / outcome | Required boundary for extraction |
| --- | --- | --- |
| Business-key identity after projection loss | Inventory's known nonzero-version append rejects a missing model; its administrative rebuild restores the original stream identity. Purchasing edits addressed by known stream ID fail when either required view is missing/behind and do not append facts. Both creation paths still rely on a projection's unique business-key index. | The event store protects **stream ID plus expected version**, not business-key uniqueness after projection loss. Do not extract a generic “missing projection means new aggregate” operation or advertise projection-independent uniqueness. Business-key ownership remains module policy. |
| Repair writer coordination | Existing races prove repair waits for earlier writers and later writers load after repair. The new cross-position test also blocks another existing position and a newly created position in the same Organization; committed reads and creation in another Organization proceed while repair is paused. | Retain the Organization-wide Inventory gate and its transaction lifetime. Do not narrow it without a tested discovery/creation protocol and lock ordering. All participating writers, including broker handlers, must acquire it before reading state. |
| Replay and retained reservation cost | New literal retained histories exercise 1,002 and 10,002 events, with receipt-only or reserve/release facts. Full replay, repair, verification and the next inline append have independently expected quantities/versions. Serial diagnostic samples reveal substantially different costs by state shape. | Small-stream, offline full reconstruction remains the supported repair mechanism. Reservation collection growth is a sample-specific limitation, not a generic event-store guarantee. No arbitrary-size, constant-memory or negligible-cost replay promise. |
| Reducer semantics | Retained opening/receipt/correction fixtures, reservation/release Contracts tests and Purchasing literal v1/multiple-item fixtures assert concrete quantities, reasons, line counts and totals. Large-history expectations are independent of the reducers. | Live and inline paths may share evolution, but equivalence alone is insufficient. Preserve independent semantic fixtures and keep command policy out of historical reduction. |
| Atomic durable workflow | Existing reserve/release fault matrices are expanded to fail stream-header advancement as well as events, current projection, audit, semantic receipt, inbox and outbox. They observe no stock/history effect or published outcome before repair and successful redrive of the same command afterward. Purchasing's matrix now also fails header/event writes, alongside each inline view and audit. | Transaction ownership stays with the owning use case. Extract staging mechanics only; no hidden independent commit, direct broker publication during append, or implied cross-module transaction. |
| Required payload compatibility | Inventory explicitly marks every current event field `JsonRequired`; Purchasing requires non-optional constructor parameters. Both reject a missing required payload field through their module read seam. Retained v1 fixtures still hydrate; the optional ended-reservation flag retains its pre-release default. | These mechanisms currently have the same missing-field meaning for supported event payloads. A future common codec must preserve required versus deliberately optional fields and their fixtures, not infer compatibility from CLR naming. No fake v2 or generic upcaster is needed. |

## Identity and repair restrictions

Projection-dependent uniqueness is **not fixed** by this gate. In Inventory, deleting the lookup followed by an expected-version-zero receipt can open another stream for the same item/location. In Purchasing, deleting the code-bearing write view followed by draft creation can open another stream for the same code. Stream-header uniqueness cannot prevent this when the new stream ID differs. The failure is outside normal append/rebuild, but it must not be concealed by a library abstraction.

For known projection loss/corruption, disable affected human **and broker** writes before maintenance; rejecting known nonzero versions is not a universal creation guard. Keep required lookup rows during normal lifecycle retirement. Inventory repair requires the original stream ID; business-key temporal reads cannot locate a deleted lookup until repair completes. Discovering lost identities is a privileged owning-module maintenance responsibility, not a cross-module table read or an event-store “get or create” fallback.

Purchasing has no supported administrative projection rebuild yet. Its second-aggregate capability proof supports live reconstruction by known ID and fail-closed edits, **not automated recovery of deleted views**. Restoring a consistent database backup or implementing and proving a Purchasing-owned repair operation is required before that scenario can be supported. Do not call live reading a repair procedure.

If an adopting product must keep accepting creation during lookup loss, first introduce and test a module-owned durable identity constraint independent of the rebuildable view. Do not put Stock Item/Location or Purchase Order code into the domain-neutral stream header, scan all histories in ordinary commands, or rely on a UUID generated afresh for each creation. That change needs its own reviewed migration/concurrency/repair proof.

## Replay-cost observations

The diagnostic test measures the complete PostgreSQL-backed Contract call: reading/materialization, JSON decode, evolution and (for repair/append) persistence/commit. Each sample uses a fresh DI scope. A small historical read warms the read path; three full replays follow, then repair, verification and the next append. Setup seeds literal recorded facts and advances the head while deliberately leaving the inline view behind. Product assertions use Contracts only.

Focused serial measurements on the development VM, .NET SDK 10.0.112 / PostgreSQL 18.6, on 2026-10-02:

| Retained history | Full replay, three samples | Process allocation per replay | First repair | Next inline receipt |
| --- | --- | --- | --- | --- |
| 1,002 events: receipts only | 3.7–9.1 ms | about 1.3 MB | 7.0 ms | 5.2 ms |
| 10,002 events: receipts only | 21.6–26.7 ms | about 11.5 MB | 42.6 ms | 6.2 ms |
| 1,002 events: 500 reserve/release pairs | 9.7–13.9 ms | about 5.5 MB | 44.5 ms | 9.0 ms |
| 10,002 events: 5,000 reserve/release pairs | 542.6–895.7 ms | about 415.6 MB | 554.3 ms | 58.4 ms |

Allocation is `GC.GetTotalAllocatedBytes` delta for the **whole test process**, not retained heap, peak working set, or isolated reducer memory. MB is decimal. Serial focused execution avoids other test cases overlapping; native runner/container overhead may remain. These are observations, not latency assertions, capacity certification or a production SLO. CI asserts semantic results and reports diagnostics without hardware-dependent thresholds.

The immutable reservation array is copied when facts add or release children, and ended children remain for semantic identity. The observed growth is consistent with quadratic copy work as the collection grows. This is a domain representation problem; a shadow rebuild merely preserves that work, and snapshots do not eliminate inline model growth or later writes. Do not copy this array shape into an event-sourcing library. A bounded-keyed/persistent collection or module-owned lifecycle/compaction design is a future measured optimization requiring retained-identity and replay proofs. Deleting ended children is not an acceptable shortcut.

Full repair holds the Organization gate for its entire operation, including decode, reduction, replacement, audit and commit. It allocates full histories plus candidate state; cancellation/failure starts reconstruction again. Retain the existing atomic replacement and full-retry proofs. Supported operation remains bounded local maintenance; before larger/higher-churn deployment, define memory and write-unavailability budgets and measure the actual payload/child mix on the target footprint. There is no automatic stream-size cap or production-scale support established here.

## Findings for the template and future libraries

- **Proven:** one owning-module transaction can cover stream CAS, immutable events, multiple independent inline views and audit; the actual Inventory workflow adds semantic receipt/inbox/outbox to that same boundary. Repair coordination includes streams created while repair is in progress, not only the selected stream.
- **Rejected:** event count alone predicts replay cost; successful live/inline equivalence proves semantics; stream-ID uniqueness implies business-key uniqueness; read-time hydration repairs persisted views; a shadow job solves retained-child growth.
- **Retained limits:** projection-dependent identity, coarse Organization repair blocking, full reconstruction/retry, retained reservation growth, no Purchasing repair Contract, and targeted rather than arbitrary self-consistent corruption detection. The original reserve receipt still lacks target stream/item/location identity; ambiguous wrong-target versus lost-lookup release remains a technical failure, not fabricated compensation.
- **Extraction candidates:** codec/alias registry, ordered range validation/hydration, stream CAS/envelope staging, explicit projector coordination and native transaction coordination where actually shared. Projection identity strategy, reservation retention, order policy, authorization, audit choice, broker semantic idempotency and operational repair admission remain module-owned.
- **Deferred:** generic upcaster graph, checkpoint snapshots, resumable shadow rebuild, online schema/projection revision switching, multi-stream projection/transaction framework and competing async projection workers. Measurement does not authorize these features.

## Verification

The PostgreSQL and RabbitMQ CI jobs already execute these test projects. No new CI lane, dependency, container type, internal visibility grant, HTTP seam or production abstraction is required.

Reproduce serial cost observations with the normal Podman/Docker environment from [the test guide](../../tests/README.md):

```bash
DOTNET_PROCESSOR_COUNT=4 dotnet test --project tests/PersistenceTests/PersistenceTests.csproj --filter-method '*Rebuild_LargeRetainedHistory*' --max-threads 1 --show-live-output on --output Detailed
```

On 2026-10-02, the full PostgreSQL lane passed all **163** cases, including the five new cost/coordination cases, four Purchasing missing/lagging-view cases, two additional Purchasing stream/event fault cases and the Inventory missing-required-payload case. The focused broker run passed **34** cases: the expanded reserve/release matrices, retained release-identity regressions, and existing Sales/recovery atomic-participant cases selected by the same filter. This is not a claim that the entire Broker or Topology suite ran for this increment. Architecture passed **21**, application passed **27**, and repository formatting, native semantic style and analyzers passed. No HTTP/AppHost behavior changed, so no new Topology lane was introduced or exercised here.

## Primary documentation checked

- [.NET required JSON properties and constructor parameters](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/required-properties): required presence is distinct from nullability; optional defaults must preserve retained meaning.
- [PostgreSQL 18 advisory locks](https://www.postgresql.org/docs/18/explicit-locking.html#ADVISORY-LOCKS): these are application-cooperative locks, and transaction-level locks end with the transaction. They do not protect against arbitrary SQL that bypasses the protocol.
- [Marten projection rebuilding](https://martendb.io/events/projections/rebuilding.html): a reference for recovery lifecycle distinctions, not evidence that this implementation inherits Marten's daemon/rebuild capabilities or requires its framework.
