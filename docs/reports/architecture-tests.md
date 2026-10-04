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

These 13 tests are repository/template policy, editable by consumers. ArchUnitNET adds no
runtime dependency to the libraries or samples. No new reusable product mechanism was
proven, and library interfaces/implementations were not changed by the audit.

The earlier 22-test execution remains a historical tooling result. Current test counts,
verification, gaps and owner-review files are recorded in [the audit report](test-audit.md).
