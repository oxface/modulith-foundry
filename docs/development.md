# Development and verification

`ModulithFoundry.slnx` contains 12 active projects: independent ActorIdentity and Tenancy
libraries, the EF ownership utility, two finite console samples and their proof suites.
The archived solution is independent. Root build defaults target .NET 10; central package
management pins test/DI packages, EF Core Relational/Design, native Npgsql, Testcontainers and
test-only ArchUnitNET. The local tool manifest also pins native `dotnet-ef` 10.0.12.

## Repository tooling

Follow [the .NET conventions](conventions/dotnet.md) when changing C# code.

```bash
dotnet tool restore
npm ci --prefix tools/repository
python3 tools/repository/verify-archive.py
dotnet csharpier check . --include-generated
```

Root formatting excludes `archive/` through `.csharpierignore`; archived formatting is
verified separately against its original configuration. CSharpier owns C#/XML layout.
Semantic style and analyzer checks operate on a concrete solution.

Lefthook checks root formatting, active style/analyzers/context and EF model tests/dependencies,
and the archived semantic/architecture baseline. Restore the active and archived solutions before
using hooks. Container suites stay outside local commit hooks.

## Active context lane

Run from the repository root:

```bash
dotnet restore ModulithFoundry.slnx
dotnet csharpier check . --include-generated
dotnet format style ModulithFoundry.slnx --verify-no-changes --no-restore
dotnet format analyzers ModulithFoundry.slnx --verify-no-changes --no-restore
dotnet build ModulithFoundry.slnx --no-restore
dotnet test --project tests/ArchitectureTests/ArchitectureTests.csproj --no-build --no-restore
dotnet test --project tests/ActorIdentityTests/ActorIdentityTests.csproj --no-build --no-restore
dotnet test --project tests/TenantTests/TenantTests.csproj --no-build --no-restore
dotnet test --project samples/Wholesale/ContextDemo.Tests/ContextDemo.Tests.csproj --no-build --no-restore
dotnet test --project tests/EntityFrameworkCoreTests/EntityFrameworkCoreTests.csproj --no-build --no-restore
dotnet run --project samples/Wholesale/ContextDemo/ContextDemo.csproj --no-build --no-restore
```

These checks require no containers, identity provider or personal credentials. The active
architecture suite uses ArchUnitNET for compiled type dependencies. Three short declaration
tests read the runtime libraries' copied project files with native XML APIs: both cores
allow no package/project/extra-framework references, and persistence allows only an explicit
EF Core Relational package reference. No restored-graph parser or exact transitive-package
whitelist is maintained. These checks inspect direct declarations, not evaluated MSBuild
imports or transitive dependencies.

Two container-free sample policies also inspect actual Inventory/Sales models and native
migration operations for module-owned schemas, explicit ownership/global classification,
Sales's tenant-bearing customer/address relationship and snapshot/model consistency.
They build design-time contexts without connecting to a database.

Standalone adoption is exercised by the real actor-only, tenancy-only and GUID EF consumers;
ordinary restore/build and their behavior tests remain part of CI. Sample project/package
graphs are editable composition rather than exact test snapshots. Architecture policies are
repository-owned; consumers select their own module structure.

CI's Active context lane runs architecture tests, style, analyzers, build, the other four
container-free test projects and the context console. Root formatter verification remains
in the repository-check lane. See [the test audit](reports/test-audit.md),
[architecture checks](reports/architecture-tests.md), [the E1 split report](reports/e1-identity-split.md)
and [the E2.1 report](reports/e2-1-tenant-ownership.md).

## Active PostgreSQL ownership lane

After restoring and building the active solution, run with a reachable Docker-compatible
container engine. Tests start disposable PostgreSQL 18.6 instances and keep the resource
reaper enabled; no application process or personal database credentials are required.

```bash
dotnet test --project tests/PersistenceTests/PersistenceTests.csproj --no-build --no-restore
dotnet test --project samples/Wholesale/PersistenceDemo.Tests/PersistenceDemo.Tests.csproj --no-build --no-restore
```

For this repository's rootless Podman setup, prefix each command with:

```bash
DOCKER_HOST=unix:///run/user/1000/podman/podman.sock \
TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/run/user/1000/podman/podman.sock \
DOTNET_PROCESSOR_COUNT=4
```

Use your actual user socket path when it differs. The console smoke proof starts its own
finite child process from the source checkout and passes the disposable connection through
its environment. The sample executable must have been built in the same configuration.
CI's separate Active PostgreSQL ownership lane runs both suites. See
[the E2.1 report](reports/e2-1-tenant-ownership.md),
[the E2.2 report](reports/e2-2-module-migrations.md),
[the E2.3 relationship report](reports/e2-3-tenant-relationships.md) and
[sample run instructions](../samples/Wholesale/PersistenceDemo/README.md).

## Archived backend

Run the archived solution and tests from `archive/proof-sample`; see
[the archive guide](../archive/README.md) for exact commands and provenance. CI's Fast,
PostgreSQL, RabbitMQ, and Topology lanes use that working directory. Their results are
reference evidence, not tests of libraries that have not been implemented.

Use the archived test guide's Docker/Podman setup for container suites. It includes socket
configuration, keeps the resource reaper enabled, and explains runner concurrency limits.
No test may require personal credentials or an undeclared local process.

## New library and sample checks

Add each test to a documented CI lane in the increment that introduces it. Pure decision
and interface tests run without containers. PostgreSQL proves storage/transaction semantics;
real broker tests prove delivery behavior; Topology tests prove host wiring. Test through the
public library interface or sample Contracts. Fault setup may use SQL/process barriers;
product assertions use the consumer-facing seam.

Independence checks must build and exercise minimal consumers with only the selected
segment and its documented dependencies. Include separate state-stored, event-sourced,
and reliable-messaging compositions as those capabilities arrive. Sample architecture
policy should also demonstrate a configurable alternative and reject a deliberate violation.
