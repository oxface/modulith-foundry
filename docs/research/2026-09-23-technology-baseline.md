# Technology Baseline Research

Status: Research input for architecture review; not an implementation decision record.

As of: 2026-09-23

## Purpose and method

This note records a time-specific dependency baseline for the proposed .NET modular monolith. It uses only first-party documentation, official project repositories and releases, and official NuGet metadata. Versions will move; the repository should pin exact versions and use an explicit update policy rather than treating this note as permanently current.

The recommendations assume one .NET process, one deployment, PostgreSQL schemas owned by modules, Aspire for local orchestration, and no application scaffolding until the architecture plan is approved.

## Executive recommendation

- Use .NET 10 LTS, ASP.NET Core 10, and EF Core 10 at the current `10.0.12` servicing level. Pin Ubuntu's maintained SDK `10.0.112` feature band and centralize package versions.
- Use PostgreSQL `18.6` and Npgsql/EF provider `10.0.3`. Give every module its own schema, DbContext, migrations, and migrations-history table.
- Use Aspire `13.5.4` for local topology, observability defaults, and whole-system tests. Treat its latest-only support policy as an upgrade obligation, not as application architecture.
- Use ArchUnitNET `0.13.4` for architecture tests. It is active and substantially more capable than the older NetArchTest package, but its pre-1.0 status is a pin-and-test risk.
- Use Testcontainers for .NET `4.15.0` for real PostgreSQL integration tests. Pin all container image tags or digests.
- Use ASP.NET Core's generic JWT bearer support against Keycloak `26.7.4`; do not put a Keycloak-specific adapter into business modules.
- Keep Rebus `8.9.4` on the shortlist, but do not select its PostgreSQL transport/outbox path until a focused reliability spike resolves an open transaction-ordering concern in `Rebus.PostgreSql`.
- Defer Redis until a concrete cache, coordination, or ephemeral-data use case exists. Redis 8 has a three-license model requiring an explicit legal/operational choice.
- Keep Mailpit `1.31.2` local/test-only.
- Defer Kubernetes manifests and Flux until the actual deployment target and operator model are known.

## Version, license, and lifecycle matrix

| Component | Current stable baseline | License | Lifecycle signal | Recommendation |
| --- | --- | --- | --- | --- |
| .NET runtime / ASP.NET Core | 10.0.12 | MIT | .NET 10 LTS supported to 2028-11-14; current patches are required for support | Adopt |
| .NET SDK | 10.0.112 | MIT | Serviced 1xx compatibility band carrying runtime 10.0.12 | Pin in `global.json` with `latestPatch` |
| EF Core | 10.0.12 | MIT | Shares the .NET 10 LTS generation | Adopt |
| Aspire | 13.5.4 | MIT | Only the latest Aspire feature release is supported; 13.5 is current | Adopt for development/testing with an upgrade budget |
| PostgreSQL | 18.6 | PostgreSQL License | Major 18 supported to 2030-11-14; project recommends the current minor | Adopt |
| Npgsql + EF provider | 10.0.3 | PostgreSQL License | Aligns with .NET/EF 10; provider requires EF Core `>=10.0.4 <11.0.0` | Adopt |
| Redis | 8.10.2 latest feature; 8.2.10 extended | RSALv2, SSPLv1, or AGPLv3 for Redis 8+ | 8.2 extended support is listed through 2030-09-01 | Defer; if needed, prefer 8.2.10 extended after license review |
| Keycloak | 26.7.4 | Apache-2.0 | Only the latest minor is supported; frequent upgrades are normal | Propose for local/product identity |
| Mailpit | 1.31.2 | MIT | No LTS promise; current release contains a security fix | Local/test only |
| Rebus | 8.9.4 | MIT | Active releases, but no long-term support promise | Shortlist |
| Rebus.PostgreSql | 9.1.1 | MIT | Last package release 2024-05; open 2026 outbox issue | Do not approve without a spike |
| Testcontainers for .NET | 4.15.0 | MIT | Active | Adopt for integration tests |
| ArchUnitNET | 0.13.4 | Apache-2.0 | Active, but pre-1.0 | Adopt and pin |
| NetArchTest.Rules | 1.3.2 | MIT | Last package release 2021-05 | Do not select for a new baseline |
| OpenTelemetry .NET | 1.18.0 | Apache-2.0 | Traces, metrics, and logs are stable | Adopt through Aspire service defaults |
| Kubernetes | 1.37.0 | Apache-2.0 | Three active minor branches; roughly one year of patch support per minor | Defer to target platform |
| Flux | 2.9.5 | Apache-2.0 | Fast release cadence; project supports a limited set of recent minors | Defer to target platform |

## .NET, ASP.NET Core, and EF Core

### Findings

.NET `10.0.12` was released on 2026-09-08 with SDKs `10.0.112` and `10.0.401`; both carry the same 10.0.12 runtime. The hundreds digit identifies a quarterly SDK tooling feature band, not a runtime or target-framework generation. Ubuntu's distribution packages remain on the maintained 1xx compatibility band, while later bands primarily advance SDK components such as MSBuild, Roslyn, NuGet, templates, workloads, and CLI tooling. .NET 10 is the current LTS train and is supported through 2028-11-14. Microsoft's support policy applies the support train to .NET runtime, SDK, ASP.NET Core, and EF Core, and requires staying current on released servicing updates. The runtime, ASP.NET Core, and EF Core repositories use the MIT license.

Primary sources:

- [.NET 10.0.12 release notes](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.12/10.0.12.md)
- [.NET 10 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [.NET and .NET Core support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [.NET SDK, MSBuild, and Visual Studio versioning](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs)
- [.NET Linux distribution packaging guidance](https://github.com/dotnet/core/blob/main/linux.md)
- [EF Core 10.0.12 package metadata](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore/10.0.12)
- [EF Core 10 documentation](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew)
- [.NET runtime MIT license](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT), [ASP.NET Core MIT license](https://github.com/dotnet/aspnetcore/blob/main/LICENSE.txt), and [EF Core MIT license](https://github.com/dotnet/efcore/blob/main/LICENSE.txt)

### Recommendation

Target `net10.0`. Pin SDK `10.0.112` in `global.json` with `latestPatch` and keep NuGet versions in central package management. This keeps CLI, C# extension, and CI discovery on the Ubuntu-supported feature band without giving up current runtime servicing. Move to a later feature band only for a demonstrated SDK-tooling requirement and verify editor discovery as part of that change. A monthly dependency update should take current .NET servicing releases together rather than mixing runtime, ASP.NET Core, and EF patch levels without a reason.

Do not add a general mediator, repository, or unit-of-work package merely because the platform supports one. ASP.NET Core endpoints and EF Core can remain implementation details inside each module's vertical slices.

## Aspire

### Findings

Aspire `13.5.4` is the current patch as of the research date. `Aspire.Hosting.AppHost` and `Aspire.Hosting.Testing` publish that version. Aspire's support policy says only the latest Aspire release is supported; 13.5 superseded 13.4 in August 2026 and 13.5.4 followed in September. A C# AppHost requires the .NET 10 SDK, although it may orchestrate applications targeting supported earlier .NET versions.

Aspire Service Defaults configures OpenTelemetry, health endpoints, service discovery, and resilient HTTP defaults. Its documentation explicitly positions the project as extension-method infrastructure and warns against placing shared models or business logic there.

Primary sources:

- [Aspire.Hosting.AppHost package metadata](https://www.nuget.org/packages/Aspire.Hosting.AppHost)
- [Aspire.Hosting.Testing package metadata](https://www.nuget.org/packages/Aspire.Hosting.Testing)
- [Aspire support policy](https://aspire.dev/support/)
- [Aspire prerequisites](https://aspire.dev/get-started/prerequisites/)
- [Service Defaults documentation](https://aspire.dev/get-started/csharp-service-defaults/)
- [Aspire repository and MIT license](https://github.com/microsoft/aspire)

### Recommendation and risk

Use Aspire to describe the local process/resource graph, start dependencies, provide dashboard telemetry, and support full-topology smoke tests. Do not let the AppHost become the business composition root: the API still owns module registration, and the Aspire AppHost only orchestrates deployable processes and infrastructure.

Pin `13.5.4`, run the full-system smoke test during Aspire updates, and expect upgrades more often than .NET LTS upgrades. Do not assume that choosing Aspire commits production deployment to Kubernetes, Azure, or any particular publisher.

## PostgreSQL, Npgsql, and EF migrations

### Findings

PostgreSQL `18.6` is the current minor in major train 18. PostgreSQL major versions receive five years of support, and major 18 is supported to 2030-11-14. PostgreSQL recommends always running the current minor release. Its permissive PostgreSQL License is used by PostgreSQL and Npgsql.

Npgsql `10.0.3` and `Npgsql.EntityFrameworkCore.PostgreSQL` `10.0.3` are the current stable packages. The EF provider targets .NET 10 and declares EF Core `>=10.0.4` and `<11.0.0`.

EF Core supports customizing its migrations-history table name and schema through `MigrationsHistoryTable`. This permits each module to keep migration metadata inside its owned schema rather than sharing a global `__EFMigrationsHistory` table.

Primary sources:

- [PostgreSQL versioning policy and supported versions](https://www.postgresql.org/support/versioning/)
- [PostgreSQL License](https://www.postgresql.org/about/licence/)
- [Npgsql 10.0.3 package metadata](https://www.nuget.org/packages/Npgsql/10.0.3)
- [Npgsql EF provider 10.0.3 package metadata](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/10.0.3)
- [Npgsql EF provider license](https://github.com/npgsql/efcore.pg/blob/main/LICENSE)
- [EF Core custom migrations-history table](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/history-table)

### Recommendation

Use PostgreSQL 18.6 and provider 10.0.3. Each module should have:

- an explicitly named schema;
- its own DbContext and entity mappings;
- its own migrations assembly or clearly isolated migration set;
- its own migrations-history table in its schema;
- no mapping, query, foreign key, or migration against another module's schema.

Prefer explicit schema mapping over depending on connection `search_path`. Apply production migrations through a controlled pre-deployment operation or job, not by every application replica on startup.

## Explicit transaction across module DbContexts

### What the platform supports

EF Core supports sharing a relational transaction across multiple DbContexts, but only when the contexts share both the same `DbConnection` and the same `DbTransaction`. The documented pattern is to supply the connection externally, begin a transaction with one context, and enlist the other context with `Database.UseTransactionAsync(transaction.GetDbTransaction())`.

When a transaction already exists, EF Core creates a savepoint before `SaveChanges` where supported. A failed `SaveChanges` can return to the savepoint, but that does not replace rolling back the outer transaction when the business operation fails.

Manually initiated transactions are incompatible with implicitly invoked retrying execution strategies. If retries are enabled, EF Core requires wrapping the entire transaction unit in `CreateExecutionStrategy().ExecuteAsync(...)`. A connection interruption during commit can leave the outcome unknown; EF documents mitigation such as client-generated keys, verification, and rebuilding or retrying idempotently.

Npgsql documents that PostgreSQL does not support nested or concurrent transactions on one connection. Its distributed-transaction support is partial, lacks recovery support, and is discouraged. This design does not need a distributed transaction: all contexts use one connection and one local PostgreSQL transaction sequentially.

Primary sources:

- [EF Core transactions and cross-context transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions)
- [EF Core connection resiliency and unknown commit outcomes](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency)
- [Npgsql transactions](https://www.npgsql.org/doc/basic-usage.html#transactions)
- [Npgsql EF provider retrying execution strategy](https://www.npgsql.org/efcore/miscellaneous/other.html)

### Proposed design boundary

If a named product workflow proves it needs atomic changes in two modules, implement a narrow infrastructure-owned coordinator that:

1. Opens one `NpgsqlConnection` and one `NpgsqlTransaction`.
2. Constructs participating DbContexts over that connection.
3. Enlists each context in that transaction.
4. Invokes module capabilities sequentially.
5. Commits only after every participating operation succeeds.
6. Disposes or rolls back on exceptions and cancellation.

Module contracts must not expose `DbConnection`, `DbTransaction`, `DbContext`, or a generic unit of work. The use case owns orchestration; infrastructure owns the transaction mechanics. Do not use `TransactionScope` for this path and do not include external effects in the transaction.

### Required proof before adoption

Use production dependency-injection registrations and a real PostgreSQL Testcontainer to prove:

- both module writes commit on success;
- a constraint failure in the second module rolls back the first module's saved change;
- an exception after both `SaveChanges` calls but before commit leaves neither change;
- cancellation rolls back the entire operation;
- all participating contexts really use the same physical connection and transaction;
- audit/outbox rows written inside the operation follow the same commit or rollback;
- retry behavior does not duplicate writes;
- client-generated identifiers and idempotency rules make an unknown commit outcome safe enough for the specific workflow.

This should remain an exception for a few named, short workflows. It is not a universal cross-module unit of work.

## Redis

### Findings

Redis `8.10.2` is the current feature release. Redis `8.2.10` is the current extended-maintenance line, with the official lifecycle table listing 8.2 support through 2030-09-01. Redis 8 and later are offered under a choice of RSALv2, SSPLv1, or AGPLv3; AGPLv3 is the OSI-approved option among those three. Redis 7.2 and earlier used BSD-3-Clause.

Primary sources:

- [Redis releases](https://github.com/redis/redis/releases)
- [Redis version management and lifecycle](https://redis.io/docs/latest/operate/oss_and_stack/install/version-mgmt/)
- [Redis licensing](https://redis.io/legal/licenses/)

### Recommendation and open question

Do not add Redis to the baseline solely because it is easy to run in Aspire. First identify whether the product needs a cache, distributed coordination, rate-limit state, or another ephemeral data structure that cannot be handled adequately in process or in PostgreSQL.

If it is justified, prefer the `8.2.10` extended line and record which license option the project accepts before committing it to development or production. A permissively licensed alternative can be researched separately if license policy rules out Redis 8.

## Keycloak and ASP.NET Core authentication

### Findings

Keycloak `26.7.4` is current and Apache-2.0 licensed. Its project support policy supports only the latest minor release, and its release cadence requires regular updates. The official container and production guides distinguish development mode from an optimized production build and require production-grade hostname, TLS, proxy, database, health, and metrics configuration.

ASP.NET Core's `Microsoft.AspNetCore.Authentication.JwtBearer` current patch is `10.0.12`. Microsoft's guidance requires APIs to validate access-token signature, issuer, audience, and expiry; APIs must not accept ID tokens as access tokens. It recommends standards-based OIDC/OAuth token issuance rather than self-created production tokens. For browser applications holding sensitive tokens, the guidance favors a secure backend/BFF and HttpOnly cookies over storing tokens in browser-accessible storage.

Primary sources:

- [Keycloak downloads](https://www.keycloak.org/downloads)
- [Keycloak 26.7.4 release](https://github.com/keycloak/keycloak/releases/tag/26.7.4)
- [Keycloak Apache-2.0 license](https://github.com/keycloak/keycloak/blob/main/LICENSE.txt)
- [Keycloak supported specifications](https://www.keycloak.org/securing-apps/specifications)
- [Keycloak container guide](https://www.keycloak.org/server/containers)
- [Keycloak production configuration](https://www.keycloak.org/server/configuration-production)
- [ASP.NET Core JWT bearer authentication guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication)
- [JWT bearer 10.0.12 package metadata](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer/10.0.12)

### Recommendation

Configure the API with generic ASP.NET Core JWT bearer authentication using Keycloak authority and audience settings. Keep claim-to-application-principal mapping at the API/security edge, enforce default-deny authorization, and enforce business permissions again inside the owning module capability. Do not expose Keycloak-specific types in module contracts.

For deterministic local tests, version a development realm definition without production credentials. Keycloak is infrastructure, not a business module, and should not share application-owned tables. Decide its production database isolation and backup policy independently.

Add BFF behavior only after the test frontend's threat model and token flow require it. A minimal non-browser test client does not justify a BFF by itself.

## Mailpit

Mailpit `1.31.2` is current and MIT licensed. The release includes a security fix, reinforcing the need to pin and update even development-only services.

Primary sources:

- [Mailpit releases](https://github.com/axllent/mailpit/releases)
- [Mailpit MIT license](https://github.com/axllent/mailpit/blob/develop/LICENSE)
- [Mailpit Docker documentation](https://mailpit.axllent.org/docs/install/docker/)

Use Mailpit only for local development and integration tests. Bind its UI and SMTP ports privately, avoid real credentials or production mail, and pin the container version or digest.

## Rebus and reliable messaging

### Findings

Rebus `8.9.4` is current and MIT licensed. Rebus has an OpenTelemetry integration package, currently `1.4.0`. The core library remains a plausible small messaging choice without adopting a larger opinionated application stack.

`Rebus.PostgreSql` `9.1.1` supplies PostgreSQL transport, saga, subscription, and timeout storage. It depends on Rebus `>=8.4.2` and Npgsql `>=8.0.3`, but its latest package was published in May 2024. An open issue filed in May 2026 reports that its outbox transaction commits before saved messages are persisted and that the path fails with current .NET 10/Npgsql combinations. This is not proof that all PostgreSQL transport use is unsafe, but it blocks recommending that outbox implementation without reproduction and failure testing.

The separately published `Rebus.Outbox` `2.0.0` comes from an archived repository and has no PostgreSQL implementation. It should not be introduced as a workaround. Small third-party alternatives such as `Freakout.Rebus.PostgreSql` are pre-1.0 and impose topology constraints; they are spike candidates, not a default.

Primary sources:

- [Rebus package metadata](https://www.nuget.org/packages/Rebus/8.9.4)
- [Rebus repository and MIT license](https://github.com/rebus-org/Rebus)
- [Rebus.OpenTelemetry package metadata](https://www.nuget.org/packages/Rebus.OpenTelemetry)
- [Rebus.PostgreSql package metadata](https://www.nuget.org/packages/Rebus.PostgreSql/9.1.1)
- [Open Rebus.PostgreSql outbox transaction issue](https://github.com/rebus-org/Rebus.PostgreSql/issues/55)
- [Archived Rebus.Outbox repository](https://github.com/rebus-org/Rebus.Outbox)

### Recommendation and adoption gate

Keep Rebus on the shortlist for workflows that truly need durable, asynchronous processing. Do not add it for ordinary in-process module commands and queries. Do not let Rebus types enter module contracts.

Before selecting a PostgreSQL transport/outbox implementation, run a focused .NET 10/PostgreSQL 18 spike that proves:

- application state and outgoing messages commit atomically;
- rollback after handler or serialization failure leaves neither state nor message partially committed;
- duplicate delivery is tolerated through a durable inbox or equivalent unique message-ID rule;
- a crash before commit, during commit, and after commit has documented recovery behavior;
- delayed retries and poison-message handling are observable and operable;
- PostgreSQL connection-pool use remains bounded under worker concurrency;
- the package works against the selected Npgsql version.

Expect at-least-once delivery. Consumers must be idempotent. Do not call this exactly-once processing, and do not combine broker delivery with an external side effect under a fictional atomic boundary.

If no candidate passes this gate, a small module-owned PostgreSQL outbox/inbox dispatcher may be simpler and more auditable than adapting an unmaintained library—but that is an architecture decision and implementation spike, not a conclusion from this research alone.

## Testcontainers for .NET

Testcontainers for .NET and its PostgreSQL module are current at `4.15.0` and MIT licensed. Its official best practices recommend specifying image versions explicitly rather than relying on floating tags.

Primary sources:

- [Testcontainers 4.15.0 package metadata](https://www.nuget.org/packages/Testcontainers/4.15.0)
- [PostgreSQL module documentation](https://dotnet.testcontainers.org/modules/postgres/)
- [Testcontainers best practices](https://dotnet.testcontainers.org/api/best_practices/)
- [Testcontainers-dotnet MIT license](https://github.com/testcontainers/testcontainers-dotnet/blob/develop/LICENSE)

Use Testcontainers for module persistence tests, migration tests, shared-transaction rollback proofs, event-store concurrency tests, and outbox/inbox failure tests. Use `Aspire.Hosting.Testing` for a smaller number of whole-topology smoke tests. This separation keeps most integration tests focused and fast while proving the actual database behavior.

The build environment must provide a compatible container runtime. Pin PostgreSQL, Redis if adopted, Keycloak, and Mailpit image tags or digests.

## Architecture testing: ArchUnitNET versus NetArchTest

### Findings

`TngTech.ArchUnitNET` `0.13.4` was published in August 2026 and is Apache-2.0 licensed. It provides a richer architecture model and fluent rules, including dependency and cycle-oriented assertions. It remains pre-1.0, so upgrades may be breaking.

`NetArchTest.Rules` `1.3.2` is MIT licensed but its last package release was in May 2021. Its simpler API is adequate for some namespace and dependency rules, but its release activity is a poor default signal for a new long-lived template.

Primary sources:

- [ArchUnitNET package metadata](https://www.nuget.org/packages/TngTech.ArchUnitNET/0.13.4)
- [ArchUnitNET repository and license](https://github.com/TNG/ArchUnitNET)
- [NetArchTest package metadata](https://www.nuget.org/packages/NetArchTest.Rules/1.3.2)
- [NetArchTest repository and MIT license](https://github.com/BenMorris/NetArchTest)

### Recommendation

Choose ArchUnitNET and pin `0.13.4`. The initial rules should inspect compiled assemblies and prove that:

- module implementations may depend on other modules only through their Contracts assemblies;
- Contracts do not depend on implementation projects, applications, EF Core, Npgsql, ASP.NET Core, Rebus, or other infrastructure packages;
- implementation persistence/domain types are internal unless a reviewed reason says otherwise;
- the API composes implementations but contains no business workflow dependencies;
- cycles do not form among module contracts and implementations.

Project references and compiler visibility remain the primary enforcement. Architecture tests add understandable policy failures and catch forbidden public API types that the project graph cannot express.

## OpenTelemetry

OpenTelemetry .NET `1.18.0` is current and Apache-2.0 licensed. The official .NET documentation classifies traces, metrics, and logs as stable. Aspire Service Defaults already configures common ASP.NET Core, HTTP-client, and runtime instrumentation plus OTLP export.

Primary sources:

- [OpenTelemetry.Extensions.Hosting 1.18.0 package metadata](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting/1.18.0)
- [OpenTelemetry .NET changelog](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry/CHANGELOG.md)
- [OpenTelemetry .NET documentation](https://opentelemetry.io/docs/languages/dotnet/)
- [OpenTelemetry .NET Apache-2.0 license](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/LICENSE.TXT)

Use Aspire Service Defaults rather than introducing a custom observability abstraction. Add application `ActivitySource` and `Meter` instruments only at important workflow, message, and projection boundaries. If Rebus is adopted, evaluate its OpenTelemetry package against the chosen Rebus version.

Audit events are durable business evidence and must not be represented only as logs, traces, or metrics. Telemetry has different retention, access, sampling, and deletion semantics.

## Kubernetes and Flux: later scope only

Kubernetes `1.37.0` is the current minor. Kubernetes maintains the three most recent minor releases and generally provides about one year of patch support for each minor. Flux `2.9.5` is current; Flux also has a fast cadence and supports a limited window of recent releases. Both projects use Apache-2.0.

Primary sources:

- [Kubernetes releases and support policy](https://kubernetes.io/releases/)
- [Kubernetes release history](https://kubernetes.io/releases/release/)
- [Kubernetes Apache-2.0 license](https://github.com/kubernetes/kubernetes/blob/master/LICENSE)
- [Flux 2.9.5 release](https://github.com/fluxcd/flux2/releases/tag/v2.9.5)
- [Flux release and support information](https://fluxcd.io/flux/releases/)
- [Flux Apache-2.0 license](https://github.com/fluxcd/flux2/blob/main/LICENSE)

Do not create Kubernetes manifests or Flux resources until the hosting target, managed-service choices, ingress/TLS model, secret store, and operational ownership are selected. First establish a deployment contract independent of orchestrator:

- one OCI application image;
- startup, readiness, and liveness behavior;
- external configuration and secrets;
- an explicit migration job/process;
- backup and restore responsibilities;
- telemetry export and alert ownership;
- initial replica count and safe scaling constraints;
- managed or self-hosted PostgreSQL, identity, and any later cache/message store.

Then choose Kubernetes/Flux only if the target environment and team operations justify their cost. If Kubernetes is selected, pin to a version supported by the provider rather than blindly following upstream latest.

## License review

### Increment 1.1 implementation recheck

The first executable increment rechecked every dependency it introduced against primary package or release metadata on 2026-09-23:

| Dependency | Pinned version | License | Use |
| --- | --- | --- | --- |
| xUnit.net v3 MTP v2 | 4.0.1 | Apache-2.0 | Executable test projects on Microsoft Testing Platform; no separate VSTest adapter |
| ArchUnitNET xUnit v3 extension | 0.13.4 | Apache-2.0 | Compiled dependency rules and xUnit assertions |
| `@commitlint/cli` / conventional config | 21.2.3 / 21.2.3 | MIT | Conventional Commit validation in hooks and CI |
| Lefthook | 2.1.14 | MIT | Fast local format, architecture-test, and commit-message hooks |
| `actions/checkout` | 7.0.1 | MIT | CI checkout, pinned to the immutable release commit |
| `actions/setup-dotnet` | 6.0.0 | MIT | Installs the SDK from `global.json`, pinned to the immutable release commit |
| `actions/setup-node` | 7.0.0 | MIT | Installs Node 24.21.0 for repository-only checks, pinned to the immutable release commit |

Primary sources: [xUnit.net MTP v2 4.0.1 package](https://www.nuget.org/packages/xunit.v3.mtp-v2/4.0.1), [xUnit.net Microsoft Testing Platform guidance](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform), [ArchUnitNET 0.13.4 package](https://www.nuget.org/packages/TngTech.ArchUnitNET.xUnitV3/0.13.4), [commitlint CLI](https://www.npmjs.com/package/@commitlint/cli), [commitlint conventional config](https://www.npmjs.com/package/@commitlint/config-conventional), [Lefthook](https://www.npmjs.com/package/lefthook), [`actions/checkout` 7.0.1](https://github.com/actions/checkout/releases/tag/v7.0.1), [`actions/setup-dotnet` 6.0.0](https://github.com/actions/setup-dotnet/releases/tag/v6.0.0), and [`actions/setup-node` 7.0.0](https://github.com/actions/setup-node/releases/tag/v7.0.0).

Microsoft Testing Platform is the repository-wide test execution platform; xUnit remains the test framework, and ArchUnitNET only supplies architecture assertions. MTP was selected for its executable test-project model, deterministic compile-time extension registration, and native .NET 10 CLI support, not for any dependency from ArchUnitNET or future AI evaluations.

The Node packages are isolated under `tools/repository` with an npm lockfile because they are repository controls, not the product frontend. Vite, pnpm, Prettier, and the application JavaScript workspace remain deferred to Increment 6.1.

The proposed default application dependencies use permissive MIT, Apache-2.0, or PostgreSQL licenses. Redis is the notable exception: Redis 8's RSALv2/SSPLv1/AGPLv3 choice needs an explicit project policy decision. Container images also contain transitive operating-system packages; image scanning and software-bill-of-material generation remain deployment concerns even when the top-level project license is permissive.

The repository's own license does not automatically settle whether all optional infrastructure licenses are acceptable for the product's distribution and hosted-service model.

## Update and verification policy

After architecture approval, record exact package versions centrally and exact container tags or digests in the AppHost/deployment configuration. Automate update proposals, but merge them only after:

1. build and architecture tests;
2. module integration and migration tests;
3. shared-transaction tests if that mechanism is adopted;
4. messaging crash/retry/idempotency tests if messaging is adopted;
5. Aspire full-topology smoke tests;
6. an authentication smoke test against the pinned Keycloak image.

Patch .NET monthly. Review Aspire and Keycloak releases frequently because their supported window is narrower. Review PostgreSQL and all internet-facing development services promptly for security updates.

## Decision register produced by this research

### Ready to propose

- .NET/ASP.NET Core/EF Core 10 with current patches.
- PostgreSQL 18 with Npgsql 10 and per-module migration history.
- Aspire for local orchestration, service defaults, and topology tests.
- Testcontainers for database-level integration tests.
- ArchUnitNET for dependency-policy tests.
- Generic ASP.NET Core JWT bearer authentication against Keycloak.
- OpenTelemetry through Aspire Service Defaults.
- Mailpit only as disposable local/test infrastructure.

### Requires a proof or explicit choice

- A shared PostgreSQL transaction must pass the failure matrix for one named workflow before general use.
- Event sourcing, inline projections, and audit-event representation require a separate domain- and data-model decision; none of the packages above decides them.
- Rebus/PostgreSQL messaging requires a reliability spike, especially for outbox behavior on current .NET/Npgsql.
- Redis requires both a concrete use case and license acceptance.
- Browser token handling may require a BFF, but only after the frontend and threat model are known.

### Deliberately deferred

- Kubernetes manifests and Flux GitOps resources.
- A generic scaffold generator.
- Any general messaging, event-store, repository, mediator, or unit-of-work framework.

## Open questions for plan review

1. What failures in the earlier attempts were architectural, operational, tooling-related, or caused by over-generalization? Those should shape the proof gates and the first vertical slice.
2. Which actual product workflow, if any, needs a cross-module atomic transaction?
3. Which aggregate has a business need for event sourcing rather than ordinary state plus audit records?
4. What audit evidence, retention, access, correction/redaction, and tamper-evidence requirements apply?
5. Which workflow first justifies durable messaging, and what external effect or time boundary makes a direct in-process call unsuitable?
6. Is Redis 8 licensing acceptable, and is Redis needed at all in the first deployable example?
7. Will the test frontend be a trusted server-rendered client, a browser SPA, or something else?
8. What is the first deployment target, and who will operate databases, identity, secrets, certificates, backups, and upgrades?
