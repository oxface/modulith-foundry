# Development and verification

`ModulithFoundry.slnx` contains 33 active projects: independent ActorIdentity and Tenancy
cores, their optional ASP.NET Core adapters, the EF ownership utility, the event codec and
ordered-range validation utility, three finite console
samples, an HTTP identity/Organization host, six populated Access/Inventory/Sales module projects and
their proof suites, an Aspire AppHost, sample ServiceDefaults and a runtime composition suite.
The archived solution is independent. Root build defaults target .NET 10; central package
management pins test/DI packages, EF Core Relational/Design, native Npgsql, Testcontainers,
sample-only OpenIdConnect, test-only TestHost and ArchUnitNET. The local tool manifest also
pins native `dotnet-ef` 10.0.12.

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

Lefthook checks root formatting, active style/analyzers/context, EF model, event codec and history tests/dependencies,
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
dotnet test --project tests/ActorIdentityAspNetCoreTests/ActorIdentityAspNetCoreTests.csproj --no-build --no-restore
dotnet test --project tests/TenancyAspNetCoreTests/TenancyAspNetCoreTests.csproj --no-build --no-restore
dotnet test --project tests/TenantTests/TenantTests.csproj --no-build --no-restore
dotnet test --project samples/Wholesale/ContextDemo.Tests/ContextDemo.Tests.csproj --no-build --no-restore
dotnet test --project tests/EntityFrameworkCoreTests/EntityFrameworkCoreTests.csproj --no-build --no-restore
dotnet test --project tests/EventSerializationTests/EventSerializationTests.csproj --no-build --no-restore
dotnet test --project tests/EventHistoryTests/EventHistoryTests.csproj --no-build --no-restore
dotnet test --project samples/Wholesale/EventCodecDemo.Tests/EventCodecDemo.Tests.csproj --no-build --no-restore
dotnet run --project samples/Wholesale/ContextDemo/ContextDemo.csproj --no-build --no-restore
dotnet run --project samples/Wholesale/EventCodecDemo/EventCodecDemo.csproj --no-build --no-restore
```

These checks require no containers, identity provider or personal credentials. The active
architecture suite uses ArchUnitNET for compiled type dependencies. Seven short declaration
tests read the runtime libraries' copied project files with native XML APIs: both cores, the event codec and history utility
allow no package/project/extra-framework references, and persistence allows only an explicit
EF Core Relational package reference; each HTTP adapter permits only its corresponding core and the
native ASP.NET Core framework. No restored-graph parser or exact transitive-package
whitelist is maintained. These checks inspect direct declarations, not evaluated MSBuild
imports or transitive dependencies.

Two container-free sample policies also inspect actual Inventory/Sales models and native
migration operations for module-owned schemas, explicit ownership/global classification,
Sales's tenant-bearing customer/address relationship, explicit customer version token and
snapshot/model consistency.
They build design-time contexts without connecting to a database.

Standalone adoption is exercised by the real actor-only, tenancy-only and GUID EF consumers;
ordinary restore/build and their behavior tests remain part of CI. Sample project/package
graphs are editable composition rather than exact test snapshots. Architecture policies are
repository-owned; consumers select their own module structure.

CI's Active context lane runs architecture tests, style, analyzers, build, the other nine
container-free test projects and the context/event codec consoles. Root formatter verification remains
in the repository-check lane. See [the test audit](reports/test-audit.md),
[architecture checks](reports/architecture-tests.md), [the E1 split report](reports/e1-identity-split.md)
and [the E2.1 report](reports/e2-1-tenant-ownership.md).

The two standalone HTTP suites run native requests without a remote identity provider,
database or personal credentials. The standalone
HTTP suites use a test-only secondary scheme to prove effective scheme selection; the executable
sample has no header authentication. Actual OIDC login/callback/session topology remains
unproven here. The executable HTTP sample now requires PostgreSQL-backed Access, Inventory and Sales; initialize
its disposable demonstration data explicitly before running it with provider configuration using
[the sample guide](../samples/Wholesale/HttpIdentityDemo/README.md); fresh results and limits
are in [the E3.1 report](reports/e3-1-http-actor-identity.md) and
[the E3.2 report](reports/e3-2-http-tenancy.md). Tenancy-only proofs add requirements,
admission failures, cancellation, route/subdomain/custom selection and scoped publication.
The sample composes both adapters through guarded Inventory reads, while actor-only adoption
continues through its independent suite. Those E3.2 fixture results remain historical;
[E3.3](reports/e3-3-persisted-access.md) records persisted Access admission proofs.
[E3.4](reports/e3-4-persisted-business-ingress.md) connects admitted requests to tenant-owned
catalog rows and separate module/Contracts projects. Focused ArchUnitNET rules enforce
module/host business-call boundaries without a restored-project graph.
[E3.5](reports/e3-5-profile-mutation.md) adds cookie JSON protection, actor-bound native
antiforgery, tenant-owned Sales profile changes and explicit versioned transactions.

## Standalone event serialization and history

The sixth technical library, `ModulithFoundry.Events.Serialization`, is independently
adoptable. Its finite [two-family consumer](../samples/Wholesale/EventCodecDemo/README.md)
now composes it with the seventh library, `ModulithFoundry.Events.History`, through two project
references. Each library remains package-free and independent of the other. Build and run
the consumer directly:

```bash
dotnet build samples/Wholesale/EventCodecDemo/EventCodecDemo.csproj
dotnet run --project samples/Wholesale/EventCodecDemo/EventCodecDemo.csproj --no-build --no-restore
```

Inventory and Purchasing explicitly register their durable aliases and choose their own
native JSON contracts. The library and consumer suites above protect exact dispatch,
configuration snapshots, failure classification and literal payload compatibility with
independently expected quantities/totals. They require no database, HTTP host, identity
provider or other Foundry segment. See [the E4 report](reports/e4-event-serialization.md)
for fresh results and the boundary before stream persistence. Existing database/browser
results remain separate evidence.

[E5.1](reports/e5-1-event-history.md) validates consumer-selected ordered ranges without
copying rows or retaining a complete-history object. The consumer selects/materializes a
version/recorded-time prefix, validates metadata, then decodes and folds its selected events.
The focused range suite uses positions/timestamps with no payload generic or serialization
dependency; the combined executable uses its own row shape and native JSON envelopes.
Current versus earlier quantities/totals, equal-time inclusion and before-first absence are
observable in its output. Stream heads/timestamps are authored demonstration metadata; real
database capture and append/transaction proofs remain E5.2.
Reassess the final package division with those real native EF consumers; the validator
currently certifies only the returned range, not excluded history.

## Active PostgreSQL ownership lane

After restoring and building the active solution, run with a reachable Docker-compatible
container engine. Tests start disposable PostgreSQL 18.6 instances and keep the resource
reaper enabled; no application process or personal database credentials are required.

```bash
dotnet test --project tests/PersistenceTests/PersistenceTests.csproj --no-build --no-restore
dotnet test --project samples/Wholesale/PersistenceDemo.Tests/PersistenceDemo.Tests.csproj --no-build --no-restore
dotnet test --project samples/Wholesale/HttpIdentityDemo.Tests/HttpIdentityDemo.Tests.csproj --no-build --no-restore
dotnet test --project samples/Wholesale/EventPersistenceDemo.Tests/EventPersistenceDemo.Tests.csproj --no-build --no-restore
dotnet test --project samples/EventStorageDemo.Tests/EventStorageDemo.Tests.csproj --no-build --no-restore
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
CI's separate Active PostgreSQL ownership lane runs all five suites. The event-history suite
launches the built native executable and coordinates captured-head races through EF command
interception, with no production test hook. It also exercises module command staging,
expected-version conflicts and caller-owned rollback on actual PostgreSQL; see
[the append findings](reports/e5-2-2-native-event-append.md). The HTTP sample uses
native protected cookies and actual Access migrations, without contacting an identity
provider. Its suite moved out of container-free CI/hooks rather than retaining fake directory
implementations; both independently adoptable HTTP library suites remain container-free. See
[the E2.1 report](reports/e2-1-tenant-ownership.md),
[the E2.2 report](reports/e2-2-module-migrations.md),
[the E2.3 relationship report](reports/e2-3-tenant-relationships.md),
[the E2.4 profile-change report](reports/e2-4-versioned-profile-changes.md) and
[sample run instructions](../samples/Wholesale/PersistenceDemo/README.md), plus
[the E3.3 Access report](reports/e3-3-persisted-access.md) and
[HTTP/database setup](../samples/Wholesale/HttpIdentityDemo/README.md), and
[the native event-history consumer](../samples/Wholesale/EventPersistenceDemo/README.md).
The [independent storage consumer](../samples/EventStorageDemo/README.md) uses the mapping
segment alone with custom table names and mixed stream types; its tests use actual migrations.

## Active Aspire runtime composition lane

The [runtime guide](../samples/Wholesale/AppHost/README.md) documents required parameters,
native start/wait/setup/stop commands, dynamic endpoints and retained local data. The graph
starts PostgreSQL and the API; migrations/seeding happen only when you explicitly start
`demo-setup`. A fresh graph is live but unready until that command succeeds. ServiceDefaults
provides native request/database traces, logs and metrics, with OTLP export when configured.
It remains editable host source and is not required by any technical library.

After restoring/building the active solution, run:

```bash
dotnet tool install --global Aspire.Cli --version 13.5.4
dotnet dev-certs https
pwsh samples/Wholesale/RuntimeComposition.Tests/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium
dotnet test --project samples/Wholesale/RuntimeComposition.Tests/RuntimeComposition.Tests.csproj --no-build --no-restore
```

Use an existing matching CLI instead of reinstalling it. This suite uses native
Aspire.Hosting.Testing, real Kestrel and PostgreSQL, ephemeral storage and randomized ports.
It requires a Docker-compatible engine, native development certificate and Chromium matching
the pinned test-only Playwright package, but no running AppHost/provider or personal credentials.
The provider-free runtime case uses HTTP. Three optional-provider browser journeys use
actual HTTPS login/callback, browser cookies and same-origin fetch. They explicitly ignore
browser development-certificate trust errors; native API backchannel validation stays enabled.
On Linux without PowerShell, the package's bundled `.playwright/node/linux-x64/node` can run
its `.playwright/package/cli.js install chromium` from the same build output; no Node workspace
is required. CI uses the generated PowerShell installer with system dependencies.
For rootless Podman, set `ASPIRE_CONTAINER_RUNTIME=podman`,
`DOCKER_HOST` to your user socket and optionally `DOTNET_PROCESSOR_COUNT=4`.
Keep this container test outside commit hooks. CI has a separate Active Aspire runtime
composition lane. External provider, proxy/subdomain and production trust/session guarantees
remain outside these local proofs.

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
