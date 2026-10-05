# Active test value audit

2026-10-04. The owner requested removal of unnecessary/framework-only tests and an assessment
of custom architecture-test support. Scope: all seven active test projects. Archived tests
and fixtures remain frozen historical evidence; they were not modified or rerun.

## Decision and outcome

Keep a test when it protects a documented library choice, consumer wiring we own, or an
integration failure that cannot be established from a unit assertion. Remove tests of
framework machinery in isolation, repeated implementation details, and fixtures requiring
private annotations outside the supported consumer contract. A framework API appearing in
a test does not make it redundant: the question is which mistake in our code it would catch.

The active suite shrank from **146 to 114 cases**, removing 32 cases net. Library and sample
runtime source was unchanged. No new reusable product mechanism was proven.

| Suite | Before | Current | Maintained value |
| --- | ---: | ---: | --- |
| ActorIdentity | 21 | 19 | Input policy, actor-kind identity, explicit attribution, exactly-once establishment, concurrent publication/reads and disposal guards. |
| Tenancy | 19 | 17 | Opaque key policy, tenantless versus missing, required-tenant checks and independent accessor lifecycle. |
| ContextDemo composition | 23 | 15 | Real DI aliases/lifetimes, independently selected segments, module calls, tenant isolation and consumer admission requirements. |
| EF model/validation | 25 | 23 | Explicit helper effects, owner validation, supported-model rejection and lazy resolution through public calls. |
| Inventory PostgreSQL | 30 | 21 | Real consumer wiring, filtered reads, all save-overload guard paths, protected writes, stored-owner predicates and finite console startup/repeat behavior. |
| Independent GUID PostgreSQL | 6 | 6 | Different key/property/schema without context libraries; prevents accidental sample-specific assumptions. |
| Architecture | 22 | 13 | Ten compiled dependency prohibitions and three small runtime-project declaration policies. |

## Removed or reduced coverage

| Change | Why the removed portion did not earn maintenance |
| --- | --- |
| Remove `RestoredProject`, exact EF transitive package whitelist and sample/test graph snapshots. | They froze incidental NuGet graphs and required a custom assets reader, repository discovery and assertion abstraction. No recurring product defect requiring that machinery was demonstrated. |
| Remove two synthetic package-free fault tests and the ArchUnitNET dependency-detection test. | Mutating arrays and asserting that `Assert.Empty` throws tests xUnit; checking that ArchUnitNET recognizes the real sample dependency tests its algorithm. Nonempty selectors remain directly in the actual rules. |
| Replace nine graph-policy cases with three direct declaration cases. | Only the runtime library dependency posture is a deliberate stable rule. Samples and tests are editable consumer composition; native build and behavior tests exercise standalone usage. |
| Remove child-scope and exception/cancellation disposal demonstrations. | Native scope/`await using` behavior has no custom pipeline here. Accessor disposal guards, actual alias registration/disposal and simultaneous isolated sample operations remain tested. |
| Reduce the six actor-kind/tenant combination cases to one tenantless identified-actor rejection. | Existing human success, system attribution, anonymous rejection and anonymous tenancy-only usage already cover the other consumer policies. |
| Remove four extra whitespace cases, generated hash-code assertions, fixture schema/type assertions and 100-read inner loops. | These repeated native whitespace/record/mapping behavior and identical reads. Empty versus whitespace rejection, exact opaque identities, public ownership model effects and concurrent readers still exercise our choices. |
| Remove the fast test inspecting detached `Update`'s modified flag. | EF's flag behavior is a framework detail. Successful same-tenant detached PostgreSQL writes in both consumers still catch our validator wrongly rejecting that flag. |
| Remove the owned-entity fixture that manually forged private ownership annotations. | It bypassed the public registration interface and tied tests to internal metadata names. It did not establish a supported consumer scenario. |
| Reduce positive save-overload cases from four to two, and foreign detached-write cases from eight to two. | All four guard entry paths remain tested with foreign inserts. Positive saves exercise both consumer overrides; stored-owner update/delete predicates do not require a Cartesian product with native overload forwarding. |
| Remove dedicated native rollback demonstration and a duplicate filtered count query. | This slice owns no transaction protocol. Native rollback remains consumer-controlled; the result-array assertion already proves filter composition without another SQL count. |

## Why the remaining architecture checks are useful

[The three declaration tests](../../tests/ArchitectureTests/AdoptionDependencyTests.cs) read
copied runtime `.csproj` files with native XML APIs, without a parser/helper abstraction.
They catch an accidental unused `PackageReference`, `ProjectReference` or extra
`FrameworkReference` that compiled type analysis could miss. Examples: adding EF or ASP.NET
to ActorIdentity; adding ActorIdentity/Tenancy, Npgsql or hosting to the EF utility. They
protect our stated independently adoptable dependency posture with a small maintenance cost.

[ArchUnitNET rules](../../tests/ArchitectureTests/AssemblyDependencyTests.cs) catch actual
compiled calls/types flowing from libraries into consumer samples or forbidden segments.
Source and target selections must contain types. That guard remains because the first
implementation actually selected no types when using short assembly-name strings.

The trade-off is explicit: direct declaration checks do not evaluate shared imports,
conditional MSBuild execution, central changes or transitive packages. Removing the restored
graph reader gives up those unused-reference checks. Dependency changes still receive owner
review, ordinary restore/auditing/build, and real consumer tests; those are not claimed to
detect every unused reference. A future concrete problem can justify native evaluated-graph
tooling, without restoring a whitelist of every third-party package now.

These are repository/template policies, not compulsory rules shipped in runtime libraries.
No new reusable architecture-test library or extraction candidate was established.

## Retained integration and guard tests

PostgreSQL stored-owner checks remain essential. Our utility constructs the filter and marks
ownership as a concurrency token; a detached payload can otherwise claim the current tenant
while targeting a foreign row ID. Real update/delete rejection and fresh reads prove this
combination through our consumer wiring, rather than merely asserting EF metadata.

Cached-model tests ensure our key expression resolves against each current context. Global
saves ensure our validator does not require tenancy unnecessarily. Successful detached writes
ensure ownership validation does not block ordinary native operations. Four foreign-insert
save cases ensure both overrides protect all supported entry paths. Model classification
protects sample-owned mapping policy rather than relying on automatic library enrollment.

Argument guards and explicit enum-number checks remain because they exercise our public
input contract and the owner's persisted-number convention. Factory/accessor failures are
not assertions that native argument checking or `Enum.IsDefined` works in isolation.

## Verification, review and gaps

All seven active suites passed: **114 tests, no skips**, including **27 real PostgreSQL
cases** on disposable Testcontainers instances with the resource reaper enabled. The current
12-project build passed with zero warnings/errors. Formatting, style/analyzer and
whitespace/link checks passed; all 800 archived files passed their checksum check. Remote
CI has not been observed.

Review the simplified [declaration tests](../../tests/ArchitectureTests/AdoptionDependencyTests.cs),
their copied-project setup in [the test project](../../tests/ArchitectureTests/ArchitectureTests.csproj),
the reduced [context composition](../../samples/Wholesale/ContextDemo.Tests/CompositionTests.cs)
and [Inventory proofs](../../samples/Wholesale/PersistenceDemo.Tests/OwnershipTests.cs).
Removed private annotations are visible in the unsupported-mapping test diff. No staging
or commit was performed this turn; previous owner-staged work was preserved.

Owned mappings remain unsupported; the removed private-annotation fixture is not replaced
by a claim about a public owned-entity configuration. Custom transaction protocols and
failure/cancellation orchestration need tests when such consumer/library code exists.
Two-module persistence, relationships, migrations and version conflicts remain subsequent
E2 work. Historical 63-context, 61-E2.1 and 22-architecture results stay in their original
reports with links to this current audit, rather than being rewritten as fresh executions.

## Subsequent E2.2 evidence

The audit was checkpointed with E2.1 as `50e7933`. Its 114-case results above remain evidence
of that audited state. [E2.2](e2-2-module-migrations.md) now passes 119 active cases, including
30 PostgreSQL cases. It adds focused two-module migration/history/save-wiring proofs and
two fast schema/artifact policies, replacing the earlier slow Inventory classification case.
No generic architecture helper or synthetic framework/assertion tests were reintroduced.

## Subsequent E2.3 evidence

[E2.3](e2-3-tenant-relationships.md) passes 126 active cases, including 37 PostgreSQL cases.
Its seven new cases protect actual Sales relationship/deletion wiring, detached-edit
compatibility and existing-customer migration. The fast model/artifact policy is extended
without new cases or a generic checker. These are consumer configuration proofs, not tests
of native foreign-key machinery in isolation; E2.2's counts above remain historical.

## Subsequent E2.4 evidence

[E2.4](e2-4-versioned-profile-changes.md) passes 131 active cases, including 42 PostgreSQL
cases. Five new cases exercise the actual consumer operation: caller-controlled commit,
stale request versions with a fresh server context, a real second-save constraint fault and
fresh recovery, cancellation after the first write, and existing-data upgrade. Assertions
observe SQL state and fresh-scope results instead of merely testing native transaction APIs.
The existing fast model/artifact policies now inspect the version mapping and migration;
no extra framework-only cases or generic infrastructure were added. Earlier counts remain
historical.

## Subsequent E3.1 evidence

[E3.1](e3-1-http-actor-identity.md) passes 161 active cases, including the unchanged 42
PostgreSQL cases. Fifteen HTTP adapter cases exercise actor publication in the actual native
pipeline, including policy-selected schemes, anonymous/public behavior, mapping failure,
permissions, overlap and cancellation. Six sample cases exercise the real directory/error
composition and configured OIDC claim actions through protected cookies. Applying those
actions caught native issuer deletion that would otherwise break the sample's actor mapping.

These are adapter/consumer behavior proofs, not isolated tests that cookies or authorization
work. No null-argument matrix, reflection over private features, generic HTTP test framework
or restored graph infrastructure was introduced. Architecture grows from 15 to 24 through
new segment/consumer dependency rules and one direct native-XML adapter declaration case.
Remote provider login/callback/session topology remains outside these counts.
