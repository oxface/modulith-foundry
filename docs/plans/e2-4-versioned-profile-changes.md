# E2.4 versioned profile changes and caller-owned transactions

Status: implementation authorized on 2026-10-04 after E2.3 checkpoint `d67c7fc`.
The owner approved checkpointing E2.3 and proceeding to this next slice. All new changes
remain unstaged for owner review; no E2.4 commit is authorized.

## Outcome

Add an application-managed `long Version` to Sales's customer fixture, separate from its
stable Organization ownership token. Consumers explicitly initialize new customers at 1.
Native EF configuration marks Version as a concurrency token with `ValueGenerated.Never`;
the declared database default of 1 backfills existing customers during the incremental
`CustomerVersions` migration. Neither EF save overrides nor the library advance versions.

Introduce consumer-owned `CustomerProfileChange` data and an ordinary callable
`CustomerProfileChanges.ApplyAsync` operation. It requires an existing caller-owned Sales
transaction, loads the selected tenant's customer/address pair, supplies the request's
expected version as the native original token, explicitly advances Version and saves the
customer name. It then saves the address line and returns the new version. The caller
explicitly begins, commits or rolls back the native transaction. No transaction scope,
retry, exception translation, event dispatch or mediator is added.

Use a dedicated operation context without unrelated pending changes. On failure, roll
back and dispose that context; a database rollback does not reset accepted tracker state.
The console demonstrates the complete native transaction recipe. It promotes draft fixture
values once and leaves repeat invocation stable when values already match; this is fixture
setup, not a general idempotency mechanism.

## Focused proofs

- Positive operation: a call without an explicit transaction is rejected; staged changes
  remain invisible to a fresh scope before caller commit; committed name, address and
  version match independent expectations.
- Two callers read the same customer version. One commits; the other's stale request
  fails with native `DbUpdateConcurrencyException`. The winning profile remains intact,
  ownership remains stable and there is no automatic retry.
- A second-save database fault occurs after the customer update has actually reached
  PostgreSQL. Observe the first write inside the transaction, explicitly roll back, and
  verify original name/address/version from a fresh scope. A fresh operation then succeeds.
  The fault uses an invalid address value against the real required-column constraint.
- Cancellation after the first save is observed through a native test-only save interceptor,
  without a product fault callback. Explicit rollback uses a non-canceled cleanup token;
  fresh reads confirm no partial profile change. Test instrumentation is not runtime wiring.
- An E2.3 database containing a customer/address migrates forward with Version 1 and can
  execute the new operation. Existing historical migration tests seed the literal older
  column shape rather than ask the current model to write an obsolete schema.
- Fast sample policies inspect both ownership and version tokens, explicit version
  generation/default configuration, module-local column operations and snapshot consistency.
  Keep all current isolation/relationship proofs and actual console startup/repeat checks.

## Scope and limits

The protocol protects coordinated profile changes when every relevant writer checks and
advances the customer version. Direct child writes do not automatically advance it. Existing
fixture writes remain visible native EF examples, not a universally enforced aggregate protocol.
Profile input/admission policy stays in the consumer; no HTTP or Access behavior is introduced.

No new reusable library mechanism is expected. Native transactions and concurrency tokens
already provide the needed primitives; explicit coordination belongs to this consumer
operation. Shared cross-module transactions, ambiguous commit outcomes, production migration
coordination and provider alternatives need separate evidence. No generic unit of work or
custom persistence scope is justified by this plan.

Native references: [concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
and [transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions).
