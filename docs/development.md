# Development and verification

Library projects and their tests live under `src/Rootbolt.{Family}/`; each family has
a README and local capability documentation. Repository architecture tests and shared support
remain under `tests/`. The active solution, hooks and CI use the family paths below.
See [the layout scope](plans/library-family-layout.md).


`ModulithFoundry.slnx` contains 43 active projects: independent ActorIdentity and Tenancy
cores, their optional ASP.NET Core adapters, the EF ownership utility, the event codec and
ordered-range validation, package-free aggregate core and EF event-storage utilities, five finite console
samples, an HTTP identity/Organization host, eight populated Access/Inventory/Sales/Purchasing module projects and
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

Lefthook checks root formatting, active style/analyzers, architecture, context, EF models,
event codecs/history and the PostgreSQL rebuilding suite. Restore the active solution
before using hooks. Other container suites run through CI or explicit local commands;
archived suites are excluded from hooks.

## CI lanes

The [main workflow](../.github/workflows/ci.yml) exposes five separately named Rootbolt
family checks through a matrix, plus Wholesale consumer composition and repository integrity.
All run on every PR and push to `main`, without family path filters. A family failure does
not cancel the other families. Each test invocation restores/builds its project and
dependencies on that runner; no job consumes another job's build outputs.

| Check | Responsibility |
| --- | --- |
| Rootbolt.ActorIdentity | Core identity and native HTTP adapter proofs. |
| Rootbolt.Tenancy | Core tenant context and independent HTTP adapter proofs. |
| Rootbolt.Persistence | EF model/write validation and real PostgreSQL GUID ownership consumer. |
| Rootbolt.Events | Serialization/upcasting, ordered history integrity and independent event-codec consumer tests/executable. |
| Rootbolt.EventSourcing | Aggregate core, PostgreSQL history/rebuilding/concurrency, independent event-storage consumer and Wholesale event-sourcing adoption. |
| Wholesale consumer composition | Context tests/executable, module migrations/ownership and persisted HTTP admission/business/telemetry tests. |
| Repository integrity | Whole-solution restore/style/analyzers/build, architecture boundaries, CSharpier, commitlint and frozen archive checksums. |
| Template integrity and adoption | TypeScript generator checks and two external generated consumers against PostgreSQL. |
| Wholesale runtime | Full Aspire setup/readiness/outage and real Keycloak/Chromium journeys on relevant PRs or manual dispatch. |

Every job has a 15-minute limit, including setup/build. The
[template workflow](../.github/workflows/template.yml) runs on every PR/main push; the
[runtime workflow](../.github/workflows/sample-runtime.yml) has PR path filters and a manual
trigger, with no automatic post-merge push run. All three workflows cancel superseded runs
on the same PR/ref. Whole-solution validation remains centralized; family jobs build only
their selected dependency graphs. Library-only changes still run all five families, since
changes to a shared dependency can affect another family.

Use the always-running family, composition, repository and template checks for branch
protection. The path-filtered runtime check remains optional globally. Its scheduling
policy preserves all deployment assertions. See the
[family-lane verification report](reports/ci-family-lanes.md) for coverage accounting,
fresh executions and remaining hosted/performance limits.

## Template creation and external-consumer proof

T1 was owner-approved and checkpointed as `8ccf4c8`. On the supported Linux/glibc environment
with Node.js 24.21+ (24.x), npm and the pinned .NET SDK:

```bash
npm ci --prefix tools/template --ignore-scripts
npm --prefix tools/template run check
npm --prefix tools/template run create -- --config example.json --output /tmp/Cedar
```

The npm script resolves configuration relative to `tools/template`; output must not exist.
See [the creator guide](../tools/template/README.md) for the exact configuration, platform
requirements and failure behavior. The output is independent of the Foundry checkout and
contains no required event/messaging setup. Its README documents build, explicit migration
and the adoption journey.

For the complete creation/adoption proof, set `CATALOG_TEST_ADMIN_CONNECTION_STRING` to a
disposable PostgreSQL 18.6 server with permission to create/drop databases, then run:

```bash
npm --prefix tools/template run verify
```

The [separate CI workflow](../.github/workflows/template.yml) supplies PostgreSQL and runs
tooling checks and this proof. It covers two names/namespaces, omission, deterministic
creation, refusal/races, conflicting parent SDK configuration and real generated-consumer
journeys outside the checkout. [The checkpoint report](reports/t1-template-rehearsal.md)
distinguishes those executions from earlier foundation results. These instructions add no
new execution evidence.

## Container-free verification

Run from the repository root:

```bash
dotnet restore ModulithFoundry.slnx
dotnet csharpier check . --include-generated
dotnet format style ModulithFoundry.slnx --verify-no-changes --no-restore
dotnet format analyzers ModulithFoundry.slnx --verify-no-changes --no-restore
dotnet build ModulithFoundry.slnx --no-restore
dotnet test --project tests/ArchitectureTests/ArchitectureTests.csproj --no-build --no-restore
dotnet test --project src/Rootbolt.ActorIdentity/tests/ActorIdentityTests/ActorIdentityTests.csproj --no-build --no-restore
dotnet test --project src/Rootbolt.ActorIdentity/tests/ActorIdentityAspNetCoreTests/ActorIdentityAspNetCoreTests.csproj --no-build --no-restore
dotnet test --project src/Rootbolt.Tenancy/tests/TenancyAspNetCoreTests/TenancyAspNetCoreTests.csproj --no-build --no-restore
dotnet test --project src/Rootbolt.Tenancy/tests/TenantTests/TenantTests.csproj --no-build --no-restore
dotnet test --project samples/Wholesale/ContextDemo.Tests/ContextDemo.Tests.csproj --no-build --no-restore
dotnet test --project src/Rootbolt.Persistence/tests/EntityFrameworkCoreTests/EntityFrameworkCoreTests.csproj --no-build --no-restore
dotnet test --project src/Rootbolt.Events/tests/EventSerializationTests/EventSerializationTests.csproj --no-build --no-restore
dotnet test --project src/Rootbolt.Events/tests/EventHistoryTests/EventHistoryTests.csproj --no-build --no-restore
dotnet test --project src/Rootbolt.EventSourcing/tests/EventSourcingTests/EventSourcingTests.csproj --no-build --no-restore
dotnet test --project samples/Wholesale/EventCodecDemo.Tests/EventCodecDemo.Tests.csproj --no-build --no-restore
dotnet run --project samples/Wholesale/ContextDemo/ContextDemo.csproj --no-build --no-restore
dotnet run --project samples/Wholesale/EventCodecDemo/EventCodecDemo.csproj --no-build --no-restore
```

These checks require no containers, identity provider or personal credentials. The active
architecture suite uses ArchUnitNET for compiled type dependencies. Declaration tests read
copied project files with native XML APIs: foundation cores, event codec/history and aggregate
core allow no package/project/extra-framework references. EF ownership allows only its native
EF Relational package; EF event storage additionally references the aggregate core and DI
abstractions for its optional scoped aggregate registration helper. HTTP
adapters permit their corresponding core and the native ASP.NET Core framework. The independent
event adopter declares just aggregate core/EF storage plus its native provider/design packages.
No restored-graph parser or exact transitive-package whitelist is maintained. These checks
inspect direct declarations, not evaluated MSBuild imports or transitive dependencies.

Two container-free sample policies also inspect actual Inventory/Sales models and native
migration operations for module-owned schemas, explicit ownership/global classification,
Sales's tenant-bearing customer/address relationship, explicit customer version token and
snapshot/model consistency.
They build design-time contexts without connecting to a database.

Standalone adoption is exercised by the real actor-only, tenancy-only and GUID EF consumers;
ordinary restore/build and their behavior tests remain part of CI. Sample project/package
graphs are editable composition rather than exact test snapshots. Architecture policies are
repository-owned; consumers select their own module structure.

CI distributes these commands among the owning family, Wholesale composition and repository
integrity jobs described above. The local command list remains a convenient container-free
verification pass. See [the test audit](reports/test-audit.md),
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

The sixth technical library, `Rootbolt.Events.Serialization`, is independently
adoptable. Its finite [two-family consumer](../samples/Wholesale/EventCodecDemo/README.md)
now composes it with the seventh library, `Rootbolt.Events.History`, through two project
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

## PostgreSQL verification

ES1's reviewed [append interface](plans/es1-bounded-event-append.md) is exercised by both event
test projects below. The [Wholesale executable](../samples/Wholesale/EventPersistenceDemo/README.md)
adds a stock-issue acceptance/rejection journey through its module Contract; the
[independent storage executable](../samples/EventStorageDemo/README.md) adds a bounded counter
using captured history/direct JSON. Both visibly own native transactions/saves/commits, and
both retain their existing authored fixtures/output. No migration or template option is added.
The provided IEventStore implementation now concentrates loading/version/required-state
coordination; explicit model declarations and native save guards enforce tracked participation.
See [the store report](reports/es1-library-write-store.md) for fresh executions and limits,
and [the earlier ES1 report](reports/es1-bounded-event-append.md) for prior appender results.

After restoring and building the active solution, run with a reachable Docker-compatible
container engine. Tests start disposable PostgreSQL 18.6 instances and keep the resource
reaper enabled; no application process or personal database credentials are required.

```bash
dotnet test --project src/Rootbolt.Persistence/tests/PersistenceTests/PersistenceTests.csproj --no-build --no-restore
dotnet test --project samples/Wholesale/PersistenceDemo.Tests/PersistenceDemo.Tests.csproj --no-build --no-restore
dotnet test --project samples/Wholesale/HttpIdentityDemo.Tests/HttpIdentityDemo.Tests.csproj --no-build --no-restore
dotnet test --project samples/Wholesale/EventPersistenceDemo.Tests/EventPersistenceDemo.Tests.csproj --no-build --no-restore
dotnet test --project samples/EventStorageDemo.Tests/EventStorageDemo.Tests.csproj --no-build --no-restore
dotnet test --project src/Rootbolt.EventSourcing/tests/EventSourcingPostgresTests/EventSourcingPostgresTests.csproj --no-build --no-restore
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
CI runs these suites in Rootbolt.Persistence, Rootbolt.EventSourcing and Wholesale consumer
composition according to ownership. Per-case databases disable
connection pooling so their idle pools do not exhaust the shared container; production connection
configuration is unaffected. The event-history suite
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

## Wholesale runtime verification

The [runtime guide](../samples/Wholesale/AppHost/README.md) documents required parameters,
native start/wait/setup/stop commands, dynamic endpoints and retained local data. The graph
starts PostgreSQL and the API; migrations/seeding happen only when you explicitly start
`demo-setup`. A fresh graph is live but unready until that command succeeds. ServiceDefaults
provides native request/database traces, logs and metrics, with OTLP export when configured.
It remains editable host source and is not required by any technical library.

After restoring/building the active solution, run:

```bash
dotnet tool install --global Aspire.Cli --version 13.5.4
# On Linux, configure trust before running dev-certs, retaining OpenSSL's system roots.
export SSL_CERT_DIR="$HOME/.aspnet/dev-certs/trust:${SSL_CERT_DIR:-/etc/ssl/certs}"
dotnet dev-certs https --trust
dotnet dev-certs https --check --trust
pwsh samples/Wholesale/RuntimeComposition.Tests/bin/Debug/net10.0/playwright.ps1 install --with-deps chromium
dotnet test --project samples/Wholesale/RuntimeComposition.Tests/RuntimeComposition.Tests.csproj --no-build --no-restore
```

Use an existing matching CLI instead of reinstalling it. This suite uses native
Aspire.Hosting.Testing, real Kestrel and PostgreSQL, ephemeral storage and randomized ports.
It requires a Docker-compatible engine, a trusted native development certificate and Chromium matching
the pinned test-only Playwright package, but no running AppHost/provider or personal credentials.
The provider-free runtime client uses HTTP; Aspire's native API health check uses HTTPS.
Three optional-provider browser journeys use
actual HTTPS login/callback, browser cookies and same-origin fetch. They explicitly ignore
browser development-certificate trust errors; native API backchannel validation stays enabled.
On Linux without PowerShell, the package's bundled `.playwright/node/linux-x64/node` can run
its `.playwright/package/cli.js install chromium` from the same build output; no Node workspace
is required. CI uses the generated PowerShell installer with system dependencies.
Certificate creation alone does not establish trust: Aspire otherwise leaves Keycloak on
HTTP and HTTPS health checks may fail. CI verifies trust before launching any test resources.
For rootless Podman, set `ASPIRE_CONTAINER_RUNTIME=podman`,
`DOCKER_HOST` to your user socket and optionally `DOTNET_PROCESSOR_COUNT=4`.
Keep this container test outside commit hooks. The separate
[Sample runtime workflow](../.github/workflows/sample-runtime.yml) runs on PRs
changing `samples/Wholesale/**`, the workflow itself or the listed shared build/toolchain inputs.
It also supports manual dispatch from GitHub Actions. Library-only changes run their focused
contract, PostgreSQL and HTTP consumer proofs; they do not automatically run the complete
sample deployment. Manually dispatch this workflow when a library change warrants testing
the full sample composition. External provider, proxy/subdomain and production trust/session
guarantees remain outside these local proofs.

When adding branch protection, require the always-running CI and Template creation jobs.
A path-filtered sample workflow should not be a global required check: on unrelated changes
GitHub does not run it, leaving required checks pending. See
[GitHub's path-filter behavior](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow#using-filters-to-target-specific-paths-for-pull-request-or-push-events).
This scheduling policy preserves the sample suite and its assertions.

## Single-stream rebuilding proofs

Family-local tests exercise native stamp conflicts between writers and independent rebuilders,
aggregate-state-only maintenance saves, cancellation, rollback and fresh recovery on PostgreSQL
18.6. Wholesale exercises both module maintenance Contracts and the executable journey. A local
supported Docker/Podman runtime is required; absence is a failure, not a skipped proof.
CI's Rootbolt.EventSourcing lane and pre-commit's event-rebuilding-postgres-tests job run this suite.

~~~sh
dotnet test --project src/Rootbolt.EventSourcing/tests/EventSourcingPostgresTests/EventSourcingPostgresTests.csproj --no-build --no-restore
dotnet test --project samples/Wholesale/EventPersistenceDemo.Tests/EventPersistenceDemo.Tests.csproj --no-build --no-restore
~~~

For rootless Podman set DOCKER_HOST=unix:///run/user/1000/podman/podman.sock and
TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/run/user/1000/podman/podman.sock.
The retained TypeScript diagnostic
[node proof](../src/Rootbolt.EventSourcing/docs/proofs/es2-coordination.ts) uses Node
24.21+ and rootless Podman to replay **historical gate design** observations in a disposable
container. It is not a current library regression test and supplies no stamp-concurrency claim.
Current executable coverage lives in the family/consumer test projects above. See
[the supported EF contract](../src/Rootbolt.EventSourcing/Rootbolt.EventSourcing.EntityFrameworkCore/README.md).

## Archived backend

The archived solution and tests remain historical reference material. Active CI and commit
hooks exclude archived restore/build/style/analyzer/test commands; the repository checks
job still verifies all frozen source hashes. Do not add archive tests to active gates.
See [the archive guide](../archive/README.md) for opt-in manual commands and provenance,
and [the CI correction report](reports/ci-runtime-and-archive-scope.md) for the boundary.

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
