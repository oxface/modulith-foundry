# E5.1 selected history ranges and explicit hydration

2026-10-06. Implemented the owner-approved [selected-range revision](../plans/e5-1-event-history.md)
after E4 checkpoint `2a49ef3b`. Public interfaces remain for line-by-line review. No E5 commit
is authorized. This turn left revisions unstaged and preserved the owner's previously staged
version; the index alone does not contain this revision.

## Outcome and extraction finding

The demonstrated reusable mechanism is ordered-range integrity: contiguous positions,
required terminal version and nondecreasing recorded timestamps within the supplied range.
The same small metadata utility serves Inventory and Purchasing without payload, JSON, EF,
identity, tenancy or messaging dependencies.

The complete in-memory history object was rejected during review. Its snapshots, generic
payload wrappers, retained arrays and library-owned version/time selectors are removed.
Validation now uses one `foreach` with a last observed version and previous timestamp.
Selection belongs before validation in consumer queries, preserving a path to native EF
filtering/ordering before materialization. The final package split remains provisional until
the E5.2 database consumers demonstrate its practical value.

The archived readers supply evidence of repeated checks and native query placement. Archived
source/fixtures remain unchanged; those earlier results are historical evidence. This revision
does not implement or prove actual EF queries, commit ordering or persistence races.

## Interface and guarantee

| Type | Responsibility |
| --- | --- |
| `HistoryPosition` | Readonly stream-version/recorded-time metadata; no payload generic or required event interface. |
| Static `EventHistory.ValidateRange` | Validate exactly `(afterVersion, throughVersion]` in the supplied order. |
| `EventHistoryException` | Fixed payload-free message with failure and nullable expected/observed positions. |
| `EventHistoryFailure` | `UnexpectedVersion = 1`, `RangeMismatch = 2`, `RecordedTimeRegression = 3`. |

Bounds are nonnegative/in order; equal bounds require an empty range. A range starting after
0 expects its first row at 1. The empty `(0, 0]` range is a valid before-first selection, not
a persisted event at version 0. Invalid bounds/null input retain native argument errors.

Gaps, duplicates/reordering, missing tails and excess rows fail. Equal recorded timestamps
are accepted; regression identifies the offending row. A nonzero-start range validates
positions from its exclusive lower bound, without comparing its first timestamp to an
excluded predecessor. The consumer owns positive stored-head validation and product context.

The validator enumerates metadata once, with O(n) work and O(1) validation state. It does not
count, copy, retain or mutate rows; this is code inspection, not a measured allocation guarantee.
The caller owns stable materialized rows through validation and hydration. Snapshot/source
mutation guarantees from the first implementation are withdrawn.

Only the returned range is certified. An earlier range can succeed despite later metadata
corruption; the later selected range fails when it includes that corruption. Full-stream
integrity checks require explicit maintenance. Payload decoding and domain validation follow
range validation and retain their own errors.

## Consumer and template findings

Consumer-owned `RecordedEvent` carries the sample's position/time/serialized envelope. Both
recipes select and order rows before materialization, validate a metadata projection, decode
with their explicit codec and invoke their own fold.

For time reads, each consumer finds the highest version satisfying the cutoff within the
supplied head, then reads the complete prefix through that version. Time filtering alone
could omit intermediate positions. Current fixtures are finite authored rows; future EF
selection and captured-head races require PostgreSQL proofs.

| Read | Independently expected result |
| --- | --- |
| Inventory current, supplied head 3 | On-hand **13**, delivery reference `DELIVERY-2`. |
| Inventory version 2 / first-receipt cutoff | On-hand **10.125**, no reference; version 1 has zero on-hand. |
| Purchasing current, supplied head 3 | Same-item replacement yields one line, quantity **5**, total **62.50**. |
| Purchasing version 2 | Earlier quantity **2.5**, total **31.25**. |
| Purchasing equal-time cutoff | Both line facts participate in version order; total **62.50**. |
| Before-first time | Both consumers return no historical state. |

The actual console retains its four expected output lines. Repeated hydration does not
accumulate state or modify source payloads. There is no command, pending-event, saving or
publisher infrastructure in these folds; no artificial effect counter was introduced.

Inventory owns quantities/units/identities/reference meaning. Purchasing owns drafts, lines,
replacement, currency and totals. JSON contracts and stream row shapes remain consumer-owned.
Template material is the editable select/materialize/validate/decode/hydrate recipe; generated
template output remains E10. No reusable query-selector or generic domain hydration mechanism
was proven.

Both retained Inventory copies and all four E4 fixtures are unchanged. Two E5.1 literals
use existing v1 schemas. [Provenance](../../samples/Wholesale/EventCodecDemo/Fixtures/README.md)
distinguishes retained payloads from new literals and authored metadata/head values.

## Fresh verification

Four affected suites passed with no failures or skips:

| Suite | Cases |
| --- | ---: |
| Ordered-range validation | 16 |
| Combined consumer | 16 |
| Serialization | 16 |
| Architecture | 53 |
| **Total** | **101** |

Range tests cover damaged positions/tails, equal timestamps, nonzero/empty ranges, invalid
bounds, maximum-version arithmetic and single enumeration. Consumer tests protect actual
business results, repeat hydration, codec/domain faults, selective integrity and consumer
version bounds. The old snapshot and in-library selection tests were removed rather than
retained as framework demonstrations.

Architecture uses the existing native-XML/ArchUnitNET policies with the revised static type;
both event libraries remain independent. No restored dependency graph, descriptor snapshot
or custom architecture helper was added.

The full **33-project** active build passed with zero warnings/errors. The final range-test
project rebuild also passed after clarifying its single-enumeration test name. The standalone build
includes only the two event libraries and executable, and its console matched expected output.
CI/hooks continue to run the affected suites and executable as previously configured. These
are local results, not an observed remote CI execution.

Native style/analyzers passed. CSharpier verified **228 files**, archive integrity verified
all **800 original files**, and the two retained Inventory copies matched their originals
byte for byte. Local Markdown checking resolved **603 links in 61 active documents**;
whitespace checks passed. No staging or commit was performed during this revision.

The first E5.1 shape's 99-case execution is superseded evidence; it does not describe the
revised contract. Other context, EF behavior, PostgreSQL and browser suites retain earlier
results and were not rerun. No storage guarantee follows from these 101 memory-only cases.

## Review and remaining gaps

Review [the validator](../../src/ModulithFoundry.Events.History/EventHistory.cs),
[metadata position](../../src/ModulithFoundry.Events.History/HistoryPosition.cs),
[failure enum](../../src/ModulithFoundry.Events.History/EventHistoryFailure.cs) and
[exception](../../src/ModulithFoundry.Events.History/EventHistoryException.cs) with
[the usage guide](../../src/ModulithFoundry.Events.History/README.md).

Review [Inventory selection](../../samples/Wholesale/EventCodecDemo/Inventory/StockPositionHistoryExample.cs)
and [Purchasing selection](../../samples/Wholesale/EventCodecDemo/Purchasing/PurchaseOrderHistoryExample.cs)
with [range proofs](../../tests/EventHistoryTests/HistoryTests.cs) and
[consumer proofs](../../samples/Wholesale/EventCodecDemo.Tests/HistoryCompatibilityTests.cs).
The shared [row shape](../../samples/Wholesale/EventCodecDemo/RecordedEvent.cs) remains sample code.

E5.2 must prove actual tenant-scoped native EF selection, trustworthy captured-head reads,
stream creation/update metadata consistency, expected-version append, competing writers,
write faults, caller-owned transactions and recovery. A coherent range from the wrong stream
or tenant cannot be detected from positions alone. Required views/repair remain E6 and
reliable messaging E7. Reassess the library/package division with those real consumers.
