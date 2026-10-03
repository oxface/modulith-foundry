# Event-sourcing rebuild and extraction review

Status: Research input and local design assessment, not a new architecture decision.

As of: 2026-10-01. Scope: the Stock Position rebuild proof, its correctness boundaries, and the later optional event-sourcing library. No dependency adoption is proposed.

## Primary-source findings

### Rebuild has several meanings

Marten supports rebuilding inline and async projections using its async daemon. Its standard rebuild resets projection progress and reconstructs projection data; cancellation can leave consistent partial progress, while retrying that rebuild resets the cell again. It also exposes `RebuildSingleStreamAsync<T>` for rebuilding one stream. These are not a documented equivalent of our durable per-stream shadow job with explicit verification and promotion.

Marten's blue/green mechanism is a different operation: a new projection version writes separate tables, catches up asynchronously alongside the previous version, and traffic moves once ready. Its fetch-for-writing workflow provides consistency during that transition. The rebuild documentation also explicitly addresses suppressing repeated side effects during warm-up. We should borrow the separation of reconstruction, serving, and effects, not claim that our one-row repair implements online versioned deployment. [Marten rebuilding projections](https://martendb.io/events/projections/rebuilding).

Marten distinguishes live, inline, and async execution. Aggregate-shaped single-stream projections are common, but multiple views and multi-stream projections are valid: a persisted projection is not necessarily the domain aggregate or its only representation. [Projection overview](https://martendb.io/events/projections/), [single-stream projections and snapshots](https://martendb.io/events/projections/single-stream-projections).

### Facts, decisions, and business boundaries remain distinct

Microsoft's event-sourcing description separates rehydrating recorded events from invoking domain operations that produce new events. Events are authoritative, while materialized state is derived. It also warns that event sourcing adds operational and evolution complexity and is not justified for all CRUD workloads. [Azure Architecture Center: event sourcing](https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing).

Eric Evans defines aggregates as consistency boundaries with aggregate-wide invariants, and bounded contexts as explicit limits of model applicability. These principles concern business meaning and consistency, not a required persistence technology or inheritance tree. [DDD Reference, bounded contexts and aggregates](https://www.domainlanguage.com/wp-content/uploads/2016/05/DDD_Reference_2015-03.pdf).

Local inference: new decisions should enforce current eligibility and final-batch invariants; historical reduction should apply retained meaning without rerunning current command validation. Decoder/schema checks, ordering, and arithmetic representability remain necessary. This is our explicit design choice, not a claim that Marten universally enforces a decider pattern or permits every malformed event.

### Locks and explicit transactions are infrastructure, not domain rules

PostgreSQL advisory locks are application-defined: PostgreSQL does not automatically attach them to tables or writes. Transaction-level locks release on transaction completion; shared holders coexist, while an exclusive holder conflicts with them. Every participating writer must use the same protocol. [PostgreSQL advisory locks](https://www.postgresql.org/docs/18/explicit-locking.html#ADVISORY-LOCKS).

EF Core supports explicit transactions spanning several saves, rather than limiting atomicity to one `SaveChanges`. Convenience can later hide repetitive setup, but must not conceal the owning use case's commit boundary. [EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions).

### Outbox/inbox correctness is separate from replay

The transactional outbox stores outgoing messages and business changes in one local database transaction; a separate worker publishes committed records. Publishing first or independently retains a dual-write failure window. [Microsoft transactional outbox](https://learn.microsoft.com/en-us/azure/architecture/databases/guide/transactional-out-box-cosmos).

RabbitMQ confirms and acknowledgements provide at-least-once delivery, not end-to-end exactly-once effects. Lost confirmations can cause republishing; consumers must tolerate duplicates and acknowledge only after their durable work succeeds. [RabbitMQ reliability guide](https://www.rabbitmq.com/docs/reliability).

Local consequence: a consumer's receipt/deduplication record, business transition, and resulting outgoing records belong in one owning-module transaction. Database-effect deduplication does not make arbitrary email/HTTP effects exactly once. Rebuilding projections must not create new integration publications or repeat historical audit; an explicitly chosen new bootstrap/export workflow is separate. The existing [Rebus-specific audit](2026-09-23-rebus-idempotency-and-delivery-semantics.md) remains the package-level evidence; this review does not revalidate its pinned release defects or prescribe another library.

## Assessment of this implementation

### What the write gate actually gates

`StockPositionWriteGate` keys a transaction-level advisory lock by **Organization**, not Stock Position ID. Receipt/correction writers acquire a shared lock before loading decision state and retain it through commit. They still execute concurrently and rely on stream optimistic concurrency for competing business writes. Full reconstruction takes the exclusive lock, waits for existing writers, and prevents all participating Stock Position writes in that Organization until repair commits. Other Organizations, ordinary readers, Inventory reference-data operations, and arbitrary SQL are not gated.

This is a coherent repair barrier, not aggregate-level business serialization. Its breadth is deliberate but potentially expensive: repair reads the full history under that barrier. Keep this limitation visible; do not describe this proof as nonblocking or zero downtime. A narrower gate is justified only after stream discovery/creation and multi-stream lock ordering have been designed and tested.

Evidence: [write gate](../../modules/Inventory/Inventory/StockPositions/Persistence/StockPositionWriteGate.cs), [write store](../../modules/Inventory/Inventory/StockPositions/Persistence/StockPositionStore.cs), [rebuild service](../../modules/Inventory/Inventory/StockPositions/Rebuild/StockPositionProjectionRebuilder.cs).

### Resolution: defer persisted shadow progress

The reviewed prototype used durable shadow state, checkpoints and explicit verification/promotion. The owner chose a simpler single-call reconstruction: acquire the writer barrier, replay the committed head fully, and replace the serving model plus operational audit in one transaction. Failure before commit leaves serving state unchanged; retry starts from version one. Separate persisted progress is not required by event sourcing generally and is deferred until stream size/recovery requirements justify it.

Evidence: [projection rebuilder](../../modules/Inventory/Inventory/StockPositions/Rebuild/StockPositionProjectionRebuilder.cs), [event reader](../../modules/Inventory/Inventory/StockPositions/Persistence/StockPositionEventReader.cs).

### Concrete weaknesses and accepted limits

1. **Projection-dependent identity:** out-of-band lookup deletion plus expected-version-zero creation can open another stream for the same business key. Stop affected writes before repair; revisit durable identity before claiming arbitrary online rebuildability.
2. **Full replay cost and coarse barrier:** reconstruction uses stream-sized memory/work and blocks tenant Stock Position writers. Measure real streams before introducing narrower gates or resumable reconstruction.
3. **Shared reducer is not an independent oracle:** deterministic replay and inline evolution can share a bug. Retained literal fixtures and independently expected state assertions remain required.
4. **No durable request/progress identity:** retry replays from the beginning. An ambiguous commit response may already represent success; a new successful call creates another operational audit, not another historical business effect.
5. **Future checkpoints need semantic compatibility:** if durable resume is later adopted, identify and enforce reducer/projection/upcaster revision compatibility rather than trusting old progress after code changes.

The [8.1b correctness gate](../plans/v1-slices.md#increment-81b--event-sourcing-correctness-and-extraction-gate) revisits these limits before optional-library extraction. Keep fixes concrete and tested; do not hide unresolved identity/atomicity gaps behind reusable interfaces.

## Extraction boundary to preserve

The requested event-sourcing library must be **separate and opt-in**. State-stored modules must not require its aggregate base class, event interfaces, stream metadata, or registration to function. Keep domain state, deciders, policies, event meaning, projection definitions, organization/business-key identity, permissions, and audit choices inside the owning module.

Potential shared mechanics are codecs and explicit alias/version registration, hydration/range integrity, expected-version append coordination, and demonstrated projection/checkpoint infrastructure. Provider-specific persistence can be an optional adapter. Transaction convenience must still permit atomic event append, required inline projections, business audit, and an owning-module outbox together; it must not automatically redispatch reconstructed facts.

Validate those seams against the planned second aggregate before extracting. Keep authorization and business audit outside the technical event-sourcing library, and keep messaging reliability usable by modules with ordinary EF persistence. This is a repository design recommendation grounded in the ownership and consistency requirements above, not an instruction to build a general framework now.
