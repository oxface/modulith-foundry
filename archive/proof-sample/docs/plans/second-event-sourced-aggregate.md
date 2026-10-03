# Second event-sourced aggregate proof — 8.1

Purchasing owns a minimal Purchase Order draft and issuance. The owner confirmed this aggregate and PostgreSQL Contract test seams on 2026-10-02. Sales Orders and Replenishment Requirements remain state-stored. This is a capability proof, not a procurement roadmap or a library extraction.

## Implemented scope

- `IPurchaseOrderDrafting` creates an Organization-unique coded draft and sets/replaces a supplier-item line at an expected version. Identical line input at the correct version is unchanged; stale versions remain conflicts.
- `IPurchaseOrderIssuance` issues a nonempty draft. Issued orders cannot be edited or issued again. This records the commitment only; it does not notify a supplier, receive goods or start another workflow.
- `IPurchaseOrderQueries` reads the committed write model, reconstructs live/exact-version/recorded-time state, and reads a bounded summary list. No new HTTP routes, supplier aggregate or Inventory reference validation are included.
- The Purchasing Agent's code-defined `purchasing.purchase-orders.manage` permission guards commands and queries behind verified actor/Organization context. Accepted changes and verified-context permission denials use Purchasing audit; forged context never creates an audit for another tenant.

Supplier and item references are sample-local business codes, not Inventory Stock Item identities. One order currency, positive constrained quantities, bounded nonnegative prices and the 100-line limit keep the example coherent; they must not enter an extracted event-sourcing library.

## Persistence and evolution

Purchasing owns `event_streams` and immutable JSONB `events` in its own schema. Technical headers contain no order code, supplier or item fields. Persisted event identities come from an attribute registry, independent of CLR names. Retained literal v1 JSON and missing-required-field/unknown-schema proofs protect compatibility; no artificial v2 event or generic upcaster is added.

The immutable decision state contains draft/issued status and priced lines. The aggregate accepts decider events by evolving candidate state before retaining pending events. Historical reconstruction evolves recorded facts without today's command-limit validation. Ordinary edits load the aggregate-shaped inline model, not event history.

Two inline views commit with each append and accepted-change audit in the handler-owned PostgreSQL transaction:

1. A JSONB write document with typed stream/Organization/version/code columns and a tenant-scoped unique code index.
2. An independent summary with line count, currency, issued status, total and its own per-item amount document. It does not read pending aggregate state to advance itself.

The explicit coordinator loads each view's committed state and checks its version before applying the accepted batch. Missing/lagging required views fail; append does not silently repair them. EF stream-version concurrency and stream/event-version uniqueness protect competing appends. Failed scopes are discarded before retrying unexpected storage faults.

JSON uses Npgsql's supported [DOM mapping](https://www.npgsql.org/efcore/mapping/json.html), not its deprecated legacy POCO mapping. The code column is intentionally relational for identity lookup/indexing; this does not claim that arbitrary JSON field queries or JSON expression indexes were proven. Stream advancement uses [EF optimistic concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency).

Live/temporal readers capture a committed head, order by stream version, require contiguous prefixes and nonregressing recorded time, and reject unsupported event schemas. Recorded time is application append time, not commit time. Cutoffs are inclusive UTC and consider currently committed facts; sequence allocation is not a commit cursor. Replay never saves projections, audits historical actions or publishes effects.

## Template and extraction findings

- The explicit load/append/codec/evolution design works with a second module and a state shape other than stock quantities. No Inventory policy or implementation dependency is copied across modules.
- A second inline projection can own its reducer/state and still share one atomic append boundary. Literal totals and line counts are independent expectations, not merely comparison of two copies of the same reducer.
- The event registry, envelopes, version checks, captured-head reconstruction and projection coordination now have concrete duplication to compare at extraction. Stricter required-constructor-field handling in Purchasing is a compatibility decision to reconcile with Inventory at 8.1b, not silently hide in a shared serializer.
- Domain limits, permissions, audit action choices, summary shapes and transaction ownership remain module policy.
- Purchasing previously had only workflow audits. The first human-command use case exposed that a required system-actor field was the wrong shape: human entries now retain their User identity with no synthetic system identity, workflow entries retain their named system actor, and a native check requires exactly one source. This actor distinction belongs in future audit-envelope review, not in the event reducer.
- Identity lookup still depends on a required projection's code index. Out-of-band projection loss followed by a new same-code creation is not independently prevented; existing-stream edits fail closed. 8.1b retains this correctness item before extraction.
- JSONB documents do not make projection upgrades or arbitrary corruption automatically recoverable. No administrative rebuild, async projector, snapshot checkpoint or resumable repair job is introduced here.

## Verification lane

The existing PostgreSQL CI lane runs `tests/PersistenceTests`, including `PurchaseOrderPersistenceTests`. Tests use fresh module DI scopes and the public Contracts for product observations. Literal SQL seeds retained history or arranges database permissions, faults and a two-writer commit barrier; SQL is not a product-state assertion seam. Existing Fast architecture/application lanes remain required.

On 2026-10-02, the final PostgreSQL lane passed all 151 cases, including 14 Purchase Order cases; architecture passed 21 and application passed 27. Formatting, native semantic style and analyzers passed. The existing Purchasing reference/recovery broker lane also passed its 14 cases after module registration changed. An earlier full PostgreSQL attempt had one existing Inventory fault-setup connection terminated by PostgreSQL (`57P01`); its three-case isolated rerun and two subsequent full runs passed. The cause was not reproduced or established; no product retry, assertion waiver or claimed infrastructure fix was introduced.

After the final audit-actor schema correction, two additional real-broker requirement cases passed: repeated-operation creation and outgoing-insert rollback followed by identity-preserving redrive. These exercise the retained workflow actor under the same native actor constraint as the Purchase Order human commands.

Increment 8.1b follows this proof to reconcile the core-correctness register and supported limits before library extraction. Passing 8.1 does not authorize extraction or deployment.
