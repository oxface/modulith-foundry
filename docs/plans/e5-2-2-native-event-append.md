# E5.2.2 expected-version append and caller-owned transactions

Status: owner-authorized scope implemented, 2026-10-06, after E5.2.1 checkpoint `4f4d5b2`.
Implementation remains unstaged for review; no commit is authorized.
See [the implementation findings and fresh verification](../reports/e5-2-2-native-event-append.md).
Read [the extraction plan](library-extraction.md), [design](../design.md) and
[the read findings](../reports/e5-2-1-native-event-history.md) alongside this proposal.

## Outcome and extraction gate

Replace the read slice's assumed atomic writer with executable module-owned command staging.
Prove creation and expected-version append for Inventory and Purchasing through native EF,
while the consumer explicitly begins its module transaction, saves, commits and disposes.
No append operation independently saves, commits, retries or publishes.

Start with consumer-owned implementations using the existing codec, range validator and EF
ownership utility. Compare their actual technical staging/concurrency logic afterward. Native
EF may already supply the necessary mechanism; only repeated complexity beyond ordinary EF
configuration earns a proposed reusable append utility. No new Foundry interface, package,
generic repository or custom transaction scope is selected in advance. No new reusable
mechanism is proven by this proposal.

## Proposed consumer-facing capability

Add business command Contracts and scoped internal implementations to the existing modules:

- Inventory: stage opening a stock position at an explicit stream/item/location identity,
  then stage a batch of positive receipts with optional delivery references.
- Purchasing: stage drafting an order at an explicit stream identity, then stage a batch
  of line changes; later facts for the same item replace the line, as in the read model.

Creation requests express expected version 0, meaning the caller expects no owned stream.
Existing-stream commands require a positive expected version. Zero remains a protocol boundary,
never a persisted stream/event version. Command DTOs carry business values, stream identity
and expected version; the established tenant supplies ownership. They accept no caller-selected
tenant or actor and expose no EF rows or JSON envelopes. Actor/audit policy remains a later
explicit participant rather than a new dependency here.

Use module-local decision logic to deliberately produce a finite typed event batch. Validate
the command and evolve the proposed state before changing tracked persistence entries; do not
populate events by scanning entities or conventions. Rich business behavior stays in the
module; no compulsory aggregate base or Foundry domain-event interface is introduced.

Proposed result shape: module-specific Staged, NotFound and Conflict outcomes. Staged carries
the proposed business result/version and explicitly does not mean durable success. The caller
must also handle save-time concurrency: a successful preflight does not prevent another writer
from winning afterward. Exact command/result names are implementation-review details, not
selected library abstractions.

## Transaction and staging protocol

The consumer registers each native scoped DbContext/provider and explicitly binds the module's
command interface. Its business calls use Contracts; its orchestration uses the existing native
composition exception to control that same owning context. A representative flow is:

```csharp
await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
var staged = await commands.StageReceiptsAsync(request, recordedAt, cancellationToken);
// Handle a preflight NotFound/Conflict without saving. Staged is a proposal only.
await database.SaveChangesAsync(cancellationToken);
await transaction.CommitAsync(cancellationToken);
// Report durable success only after commit; dispose this operation scope.
```

The interface name in this example is illustrative. Production usage must also make rollback
and scope disposal visible on failure. Publishing, auditing and required views are not silently
attached to this flow.

1. Require an active native transaction and established tenancy before write preparation.
   The caller starts it before loading/deciding and keeps it through commit. No worker gate,
   advisory lock, hidden transaction or library-managed transaction nesting is needed here.
2. For creation, stage a new positive-version header and the explicit first event batch.
   The owned header key arbitrates competing creation of that same tenant/stream identity.
3. For an existing stream, load its owned header, compare the expected version and reconstruct
   that expected prefix through the existing read/validation/codec path. A missing, damaged
   or stale stream must not be treated as permission to create or silently rebuild a view.
4. Preserve the original expected version in native EF's concurrency predicate. Advance the
   header by the batch count using checked arithmetic; assign contiguous event positions and
   explicit event identities, serialize deliberately, and stage envelopes in that context.
5. The caller supplies a UTC recorded timestamp for the batch. Equal timestamps are valid;
   reject regression against the observed header before staging. Creation/update times must
   match the first/last staged events. This is append recorded time, not database commit order.
6. Save header and event changes in the caller's transaction. More than one explicit save may
   participate in that transaction, but only commit makes the operation durable. A header
   save or any event-write failure must leave no committed partial history after rollback.

Bound the initial supported lifecycle to one append batch per stream per operation context.
Reject staging an already-tracked stream rather than silently reusing stale original values.
Use native tracking for that check; do not keep a separate set of previously staged stream IDs.
Manually clearing/detaching tracking mid-operation is outside the recipe, not a lifecycle
that the command implementation polices. After rollback/failure, use a fresh context.
An empty batch is an invalid command. Do not advance a header without events. EF contexts and
transactions are not used concurrently; competitors have independent operation scopes.

After failure or rollback, discard the entire operation context and proposed result. Native
savepoint/tracker behavior is not a general recovery contract. Retry, when a consumer chooses
it, starts with a fresh scope, current state and a new explicit business decision; no automatic
rebase or retry is provided. A commit error can be ambiguous and must not be represented as
definite rollback/safe retry without separate evidence.

## Conflict boundaries

Preflight stale versions use the module's Conflict outcome. Native DbUpdateConcurrencyException
is a save-time version conflict only when its entries are the owning module's stream headers.
PostgreSQL unique violations are conflicts only for the owning
stream header key or owned stream-position constraint; event-ID collisions, unrelated constraints,
foreign keys and other storage errors retain their actual fault meaning. Any classification
helper is consumer/native composition policy initially, with explicit constraint names and tests.

This protects one explicitly identified tenant/stream. It does not establish unique stock-item/
location pairs or purchase codes across different stream identities. Required views and durable
business-key identity are separate work. Privileged SQL can bypass the protocol; no database-wide
append-only enforcement or protection from arbitrary updates/deletes is claimed.

## Executable proofs

Extend the native EventPersistenceDemo with an actual command/commit/read journey for both
families. Preserve the existing durable fixture/read proofs as separate compatibility evidence.
Keep fixture seed helpers clearly limited to finite setup; they do not become the writer API.

Use actual PostgreSQL migrations and business Contracts to prove:

- Opening/drafting and a multi-event batch produce independently expected versions, timestamps,
  stock quantities and purchase replacement totals after fresh-context reconstruction.
- Staging changes nothing durably, and an explicit save without commit remains invisible to
  an independent reader. Commit exposes header and complete batch together.
- Missing/tenantless scope, invalid command values, negative/incorrect expected versions,
  recorded-time regression and repeated staging cannot change owned history. An existing-stream
  command for an identity present only in another tenant returns NotFound; creation can reuse
  that GUID in its own tenant without changing or disclosing the foreign stream.
- Two independent writers starting at the same head produce one complete winning batch and
  one version conflict; the loser leaves no extra rows. Competing creation of the same owned
  identity also produces one complete stream. Colliding identities across tenants remain independent.
- Deterministically injected header-write and event-write faults roll back the complete batch;
  a failure on a later envelope also prevents earlier envelopes/header changes from committing.
- An explicit first save for one stream followed by a failing save for a second stream in the
  same owning-module transaction rolls back both. Each stream has one append batch in that
  context; views/audit/outbox are not needed to invent another participant for this proof.
- Cancellation before commit rolls back staged/saved work, and a fresh operation can then
  load current state and perform a valid append. Do not infer safe replay after ambiguous commit.
- Known version conflicts are classified narrowly; unrelated storage faults propagate rather
  than being called conflicts. Native EF interception/SQL barriers coordinate races without
  sleeps, production test hooks or custom save/retry middleware.

Keep the matrix focused on these writer obligations; reuse existing codec/range/read proofs
rather than testing basic EF transaction behavior or duplicating every validation case. Run
the affected native PostgreSQL, executable, architecture and HTTP regressions, build/style/
analyzers and archive integrity. Report new results separately from archived store evidence.

## Library, template and later findings

Library work is conditional on the two writers' evidence. If native EF suffices, explicitly
report that no new reusable mechanism was proven. If a valuable shared protocol emerges,
present its small interface and consumer obligations for review before extracting it; do not
turn module Contracts, decision rules or provider-specific policy into a universal event store.

Template material is the exercised native transaction, explicit command staging, narrow conflict
handling and fresh-scope recovery recipe. The sample gains real command writes independently
of messaging. Existing state-stored behavior remains covered; materialized template/CLI remains E10.

Required inline views and repair remain E6; accepted-change audit and reliable messaging require
their explicit participants/proofs in later slices. Reservations, full purchase lifecycle,
cross-module atomic transactions and automatic compensation remain outside this increment.
