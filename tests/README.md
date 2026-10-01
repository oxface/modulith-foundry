# Tests

The current test projects map directly to CI lanes:

- `ArchitectureSupport` is a test-only library, not a lane. It discovers the conventional module project pairs, assembly names, and declared `ModuleSchema` values once for the architecture and persistence lanes.
- `ApplicationTests` is the container-free Fast lane for behavior spanning application-owned collaborators without external I/O. Its authentication tests cover issuer/audience policy and completion of a validated external identity into the linked product User stored in the BFF ticket. Its in-memory ASP.NET Core host verifies the public HTTP failure and request-validation contract without starting the Aspire topology.
- `ArchitectureTests` is the container-free Fast lane. It checks the declared solution/project graph and compiled dependencies with ArchUnitNET.
- `PersistenceTests` is the PostgreSQL lane. It uses Testcontainers with PostgreSQL 18.6 for real migration and coordination semantics, including concurrent enforcement of the cross-Membership last-administrator invariant.
- `TopologyTests` is the Aspire lane. It uses `DistributedApplicationTestingBuilder` to exercise PostgreSQL, Redis, Keycloak, the finite Migrator, dependency gating, API health, and real OTLP export to a controlled receiver. Its focused identity path drives Keycloak's authorization-code/form-post flow, verifies an opaque Redis-backed session, CSRF-protected logout, JIT identity linking, stale-ticket rejection, fail-closed Redis outage behavior, and absence of seeded secrets or token field names from API log exports.

Run them independently:

```bash
dotnet test --project tests/ApplicationTests/ApplicationTests.csproj
dotnet test --project tests/ArchitectureTests/ArchitectureTests.csproj
dotnet test --project tests/PersistenceTests/PersistenceTests.csproj
dotnet test --project tests/TopologyTests/TopologyTests.csproj
```

Docker works without additional configuration. For rootless Podman, start its user socket and point container-aware commands at it:

```bash
systemctl --user enable --now podman.socket
export DOCKER_HOST="unix://${XDG_RUNTIME_DIR}/podman/podman.sock"
export TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE="${XDG_RUNTIME_DIR}/podman/podman.sock"
```

Keep Testcontainers' resource reaper enabled. The socket override lets its container reach the same rootless Podman endpoint; Aspire owns and cleans up its topology resources independently. Do not add container suites to Lefthook: local hooks intentionally stay fast and container-free.

The repository uses xUnit v3 on Microsoft Testing Platform v2. MTP is the test execution platform; ArchUnitNET supplies architecture assertions and does not require MTP itself.

Each module implementation project declares its owned schema with the `ModuleSchema` MSBuild property. Adding a conventional `{Module}.Contracts`/`{Module}` pair makes it part of the discovered topology automatically; the Fast lane fails until its compiled and persistence test adapters cover it, and the PostgreSQL lane fails if the Migrator does not create its declared schema.

Test names use `{OperationOrRule}_{Scenario}_{ExpectedOutcome}`. Invariants with no meaningful scenario use a concise two-part fact, such as `ProjectDependencyGraph_IsAcyclic`.

Stock Position compatibility fixtures under `ApplicationTests/Fixtures/StockPositionEvents` are literal retained JSON payloads copied into test output and exercised in the Fast lane. The PostgreSQL lane covers version/time boundaries, equal-time version ordering independent of global sequence, bounded history, current authorization/tenancy, corrupt-history classification, read-only replay, and backwards-clock rejection. The existing Inventory Topology journey covers historical selectors, curated JSON/text action values, cursor paging, query errors, and permission denial through real HTTP. All remain in the existing CI lanes; no personal credentials or new infrastructure are required.

Stock Quantity Correction tests use `IStockPositionOperations` with PostgreSQL to prove append-only changes under an event-table role that cannot update/delete history, retained earlier state, curated reasons, no-op/stale behavior, quantity/reason constraints, the below-reserved invariant, tenant/permission denial, competing corrections and projection-failure rollback. Until reservation commands arrive in Slice 5, the below-reserved test seeds valid reserved inline state as its fixture; it is not a reservation/replay proof. The Fast lane retains a literal correction fixture. The Inventory HTTP journey exercises correction, its timeline, CSRF, validation and conflict/permission responses.

Administrative rebuild tests use `IStockPositionProjectionRebuilder` plus normal Stock Position reads/writes in the existing PostgreSQL CI lane. They cover full reconstruction, missing/corrupt serving-model repair, unknown/unreadable/gapped history, tenant/permission denial, replacement rollback/retry, cancellation with fresh-provider retry from the beginning, and both writer-versus-rebuild orderings. A restricted repair identity cannot write events or rewrite existing audits; a fault trigger rejects repeating business audits. SQL faults and database waits are setup/synchronization only; product outcomes are asserted through Contracts. No publication adapter is registered for replay. No durable shadow job, checkpoint, migration, topology or manual-only test lane is introduced.

Fast aggregate tests distinguish invalid final decision state from permitted intermediate batch state and validation-free historical reconstruction. A PostgreSQL history test preserves retained reason text outside today's input rules without changing its recorded text. Schema-corruption tests inject unreadable JSON values, not merely facts rejected by current business rules.

Customer tests exercise `ICustomerAdministration` through the existing PostgreSQL CI lane: fresh-scope lookup, normalized code uniqueness under concurrent creation, independent tenant codes, unresolved/forged context and current permission denial, input validation, audit-failure rollback, and safe reuse after an expected duplicate. Database triggers are fault/constraint setup only; assertions use Contracts. The focused Sales Customer topology journey uses real OIDC login and membership roles to cover creation/Location, code lookup, validation/conflict Problem Details, CSRF, tenant isolation and role revocation. No new CI lane or manual credentials are required.

Draft Sales Order proofs run in the same PostgreSQL lane through `ISalesOrderOperations`, `ICustomerAdministration` and real Inventory registration. They cover immutable snapshots after Inventory edits, one distinct-item batch (an interface decorator counts calls and delegates to real Inventory), rounded money/quantity limits, missing/inactive/foreign references, current context/permissions, concurrent tenant-local numbers, and audit-write rollback with accepted numbering gaps. The focused Sales Order topology journey covers the real authenticated HTTP ingress, snapshot response, Problem Details and role revocation. No SQL rows are asserted directly; SQL injects persistence faults only.

Submission/activity proofs extend the same Sales Contracts and PostgreSQL lane: fresh-scope submission metadata and curated reads, stale/invalid/non-draft rejection, competing submissions with one committed transition/activity, activity- or audit-write rollback and retry, actor/tenant/unresolved-context denial, and durable security-denial audit without business activity. SQL is fault/race setup only; all outcomes use Contracts. The Sales Order topology journey covers textual status/version, submission/activity, CSRF, invalid/stale/state-conflict responses and immediate role revocation. No new CI lane, internal-method tests or manual credentials are introduced.

Approval-authority tests exercise `ISalesApprovalAuthorityAdministration` and real Access Contracts in the same PostgreSQL CI lane: fresh-scope reads, amount/currency validation and numeric boundaries, competing first/replacement grants, versioned revocation/re-enablement, tenant/context isolation, stale role and suspended membership denial, ended-tenure/rejoin isolation, and audit-fault rollback. Invitations use the replaceable email-transport seam only to capture the delivered link; Access membership decisions use real persistence. SQL injects storage faults, not row assertions. The focused Sales Approval Authority topology journey covers real OIDC, management permissions, CSRF, version/conflict/validation responses, revocation and tenant isolation. No new CI lane or manual credentials are required.
