# Tests

The current test projects map directly to CI lanes:

- `ArchitectureSupport` is a test-only library, not a lane. It discovers the conventional module project pairs, assembly names, and declared `ModuleSchema` values once for the architecture and persistence lanes.
- `ArchitectureTests` is the container-free Fast lane. It checks the declared solution/project graph and compiled dependencies with ArchUnitNET.
- `PersistenceTests` is the PostgreSQL lane. It uses Testcontainers with PostgreSQL 18.6 for real migration and coordination semantics.
- `TopologyTests` is the Aspire lane. It uses `DistributedApplicationTestingBuilder` to exercise PostgreSQL, Redis, Keycloak, the finite Migrator, dependency gating, API health, and real OTLP export to a controlled receiver. Its focused identity path drives Keycloak's authorization-code/form-post flow, verifies an opaque Redis-backed session, CSRF-protected logout, JIT identity linking, stale-ticket rejection, fail-closed Redis outage behavior, issuer/audience policy, and absence of seeded secrets or token field names from API log exports.

Run them independently:

```bash
dotnet test --project tests/ArchitectureTests/ArchitectureTests.csproj
dotnet test --project tests/PersistenceTests/PersistenceTests.csproj
dotnet test --project tests/TopologyTests/TopologyTests.csproj
```

Docker works without additional configuration. For rootless Podman, start its user socket and point container-aware commands at it:

```bash
systemctl --user enable --now podman.socket
export DOCKER_HOST="unix://${XDG_RUNTIME_DIR}/podman/podman.sock"
export TESTCONTAINERS_RYUK_DISABLED=true
```

The Ryuk override is needed only by Testcontainers; Aspire owns and cleans up its topology resources. Do not add container suites to Lefthook: local hooks intentionally stay fast and container-free.

The repository uses xUnit v3 on Microsoft Testing Platform v2. MTP is the test execution platform; ArchUnitNET supplies architecture assertions and does not require MTP itself.

Each module implementation project declares its owned schema with the `ModuleSchema` MSBuild property. Adding a conventional `{Module}.Contracts`/`{Module}` pair makes it part of the discovered topology automatically; the Fast lane fails until its compiled and persistence test adapters cover it, and the PostgreSQL lane fails if the Migrator does not create its declared schema.

Test names use `{OperationOrRule}_{Scenario}_{ExpectedOutcome}`. Invariants with no meaningful scenario use a concise two-part fact, such as `ProjectDependencyGraph_IsAcyclic`.
