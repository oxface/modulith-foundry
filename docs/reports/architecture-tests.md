# Active .NET architecture checks

2026-10-04. The owner requested established .NET architecture tests, then an audit of their
value and maintenance cost. The suite uses test-only ArchUnitNET 0.13.4 with xUnit v3.
[The audit](test-audit.md) records current verification and the removed coverage.

## Current scope

[Assembly rules](../../tests/ArchitectureTests/AssemblyDependencyTests.cs) enforce ten
forbidden relationships: actor/tenancy independence, persistence independence from those
contexts, and all three libraries' independence from both sample assemblies. They load real
assemblies and assert nonempty source/target selections. This prevents accidental use of
consumer types or another independently adoptable segment; selector guards prevent silent
passes over no types. A separate test of ArchUnitNET's dependency detection was removed.

[Three declaration checks](../../tests/ArchitectureTests/AdoptionDependencyTests.cs) use
native XML APIs to inspect runtime project files copied into test output. They prevent
accidental direct package/project/extra-framework dependencies: the two context libraries
remain package-free, and persistence allows EF Core Relational without provider, context or
hosting references. The checks contain no custom parser, graph model or helper assertion API.

They do not inspect evaluated imports or the restored transitive graph. Consumers continue
to build and exercise selected segments with native .NET tooling. Unused references introduced
through shared imports and changes to third-party transitive packages are reviewed dependency
changes, not guarantees enforced by this suite. Sample/test dependency graphs are not frozen.

## What was removed and why

The earlier 22-case suite included a custom `RestoredProject` reader, an exact 13-package EF
transitive whitelist, sample/test graph snapshots and fault snapshots exercising xUnit
assertions. Those checks caught unused references, but carried disproportionate maintenance
cost and froze incidental dependency graphs. No real need for a reusable graph checker was
proven. The helper and synthetic fault tests were removed instead of promoted into a library.

The two original Python dependency scripts remain removed. CI and hooks run the .NET suite;
`verify-archive.py` remains separate checksum/provenance tooling. The archive is unchanged.

## Ownership and verification

The audit's 13 tests are repository/template policy, editable by consumers. ArchUnitNET adds no
runtime dependency to the libraries or samples. No new reusable product mechanism was
proven, and library interfaces/implementations were not changed by the audit.

The earlier 22-test execution remains a historical tooling result. Current test counts,
verification, gaps and owner-review files are recorded in [the audit report](test-audit.md).

## E2.2 sample schema policies

[Two additional cases](../../tests/ArchitectureTests/ModulePersistenceTests.cs) inspect real
Inventory/Sales models and native migration artifacts without a database. They prevent
consumer-owned entities/migration operations from drifting into another schema and detect
unclassified sample entities or a stale model snapshot. The existing Inventory classification
case moved here from the PostgreSQL suite; Sales is now classified too.

Initial migrations allow schema creation, table creation, index creation and table removal
within the module. Foreign keys and new operation kinds require a deliberate extension of
this sample policy. Raw SQL is rejected rather than interpreted. This is a small repository
policy, not an arbitrary migration parser or reusable checker. No synthetic assertion-failure
tests were added. Current suite size is 15; fresh results are in [the E2.2 report](e2-2-module-migrations.md).

## E2.3 relationship extension

The same two sample-policy cases now classify Sales's separately mapped address child and
inspect its required tenant-bearing foreign/principal keys and restricted deletion policy.
Migration inspection admits the new native unique-key operations and checks the actual
address foreign key inside table creation, including both schemas, principal table, column
pairing and deletion action. This small policy is specific to the exercised sample; unknown
operations and raw SQL still require review. No generic helper library or synthetic
assertion-failure cases were added. Suite size remains 15; [E2.3](e2-3-tenant-relationships.md)
records the fresh executions.

## E2.4 version extension

The existing Sales policy checks a separate native customer version token, explicit
`ValueGenerated.Never` and the migration default of 1. Artifact inspection admits the actual
module-local Version add/drop-column operations while retaining ownership, relationship and
snapshot consistency checks. This stays a sample-specific policy; unknown operations and
raw SQL require review. Suite size remains 15; [E2.4](e2-4-versioned-profile-changes.md)
records the new executions. No new reusable mechanism or custom test infrastructure was added.

## E3.1 independent actor HTTP adapter

The native XML declaration policy now admits the adapter's one ActorIdentity reference and
native `Microsoft.AspNetCore.App` framework reference, with no packages. Eight additional
ArchUnitNET prohibitions cover the adapter's tenancy/persistence/consumer independence and
keep the three existing libraries independent of the new HTTP sample. They protect library
adoption without imposing an exact sample project graph. No restored-project parser or
transitive package snapshot was added. Suite size is now 24;
[E3.1](e3-1-http-actor-identity.md) records fresh results.

## E3.2 independent tenancy HTTP adapter

The shared native XML HTTP-adapter declaration policy now exercises both optional adapters,
each with only its own core and the native ASP.NET Core framework. Ten additional compiled
dependency prohibitions keep Tenancy HTTP independent of ActorIdentity/Actor HTTP,
persistence and consumer assemblies, and prevent reverse dependencies on it. They enforce
adoption boundaries without freezing the sample's project graph or parsing restored assets.
The suite now has 35 cases: 28 compiled dependency rules, five declaration cases and the
two existing model/migration policies. [E3.2](e3-2-http-tenancy.md) records fresh execution.
These remain repository/template policy, with no new reusable architecture mechanism.

## E3.3 persisted Access consumer

The 35 existing cases pass unchanged with the data-backed HTTP sample. Consumer-owned EF
dependencies and Access Contracts remain outside the technical libraries; direct-declaration
and compiled dependency checks continue protecting independent adoption. Access's actual
registry/migration constraints are exercised by its PostgreSQL suite, not added to the
Inventory/Sales model checker. Folder ownership in this host is not assembly-level module
isolation. [E3.3](e3-3-persisted-access.md) records fresh results and limits.

## E3.4 populated module boundaries

[Eight new cases](../../tests/ArchitectureTests/SampleModuleBoundaryTests.cs) use the actual
Access/Inventory implementation/Contracts assemblies and host endpoint namespace. They
prevent peer-implementation/host dependencies from modules, EF/HTTP/technical-context or
implementation types leaking into Contracts, business endpoint calls into module
implementations, and technical libraries acquiring sample-module dependencies. Native host
composition and setup retain their deliberate persistence exception.

These are editable template/repository policies with nonempty selections, not native C#
visibility tests, synthetic failures or an exact restored project graph. Suite size is 43;
[the E3.4 report](e3-4-persisted-business-ingress.md) records fresh execution and limits.

## E3.5 Sales ownership

The same real-assembly ArchUnitNET checks include Sales and Sales.Contracts. Five added
implementation/peer-or-host pairs and one Contracts case bring architecture to 49 cases.
The existing technical-library and business-endpoint rules now also inspect Sales, without
a new custom parser, exact project graph or synthetic failure suite. All 49 passed freshly;
[the mutation report](e3-5-profile-mutation.md) records the full consumer proof scope.

## E4 standalone event codec

Two added cases bring the suite to **51 freshly passed cases**. The existing native XML
declaration test now checks the codec's project: no package, project or extra framework
references. One compiled-dependency case groups prohibitions on using the five other technical
segments or four consumer assemblies. The existing technical-library/module rule also includes
the codec, protecting the actual module/Contracts boundary without another case.

The finite consumer builds independently with just the codec. These are adoption policies,
not an exact sample graph or restored transitive snapshot. No new architecture helper/parser
or reusable architecture mechanism was introduced. [The E4 report](e4-event-serialization.md)
separately records the new reusable product mechanism and consumer proofs.
