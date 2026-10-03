# Development and verification

`ModulithFoundry.slnx` contains the active `ModulithFoundry.ExecutionIdentity` library, console
sample and their tests.
The archived solution is independent. Root build defaults target .NET 10; central package
management pins only the test framework and standard DI package needed by E1.

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

Lefthook checks root formatting, active style/analyzers/context tests/dependencies and the
archived semantic/architecture baseline. Restore the active and archived solutions before
using hooks. Container suites stay outside local commit hooks.

## Active context lane

Run from the repository root:

```bash
dotnet restore ModulithFoundry.slnx
python3 tools/repository/verify-context-dependencies.py
dotnet csharpier check . --include-generated
dotnet format style ModulithFoundry.slnx --verify-no-changes --no-restore
dotnet format analyzers ModulithFoundry.slnx --verify-no-changes --no-restore
dotnet build ModulithFoundry.slnx --no-restore
dotnet test --project tests/ContextTests/ContextTests.csproj --no-build --no-restore
dotnet test --project samples/Wholesale/ContextDemo.Tests/ContextDemo.Tests.csproj --no-build --no-restore
dotnet run --project samples/Wholesale/ContextDemo/ContextDemo.csproj --no-build --no-restore
```

These checks require no containers, identity provider or personal credentials. The
dependency verifier reads restored assets: the context library has no package/project
dependencies, and the sample references that library plus standard DI. Run restore again
after dependency changes so those graphs are current.

CI's Active context lane runs dependency verification, style, analyzers, build, both test
projects and the console. Root formatter verification remains in the repository-check lane.
See [the E1 report](reports/e1-tenant-actor.md) for verified behavior and limits.

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
