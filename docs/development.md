# Development and verification

The repository is in the archive-and-plan checkpoint. There is no active library/sample
solution yet. Root build defaults and central package management are retained, with no
active package versions until a project needs them.

## Repository tooling

```bash
dotnet tool restore
npm ci --prefix tools/repository
python3 tools/repository/verify-archive.py
dotnet csharpier check . --include-generated
```

Root formatting excludes `archive/` through `.csharpierignore`; archived formatting is
verified separately against its original configuration. CSharpier owns C#/XML layout.
Semantic style and analyzer checks operate on a concrete solution.

Lefthook currently checks root formatting and the archived semantic/architecture baseline.
The first active project increment must add its build, tests, formatting, and analyzer
checks to CI and update hooks. Container suites stay outside local commit hooks.

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
