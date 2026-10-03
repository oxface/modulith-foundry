# Archived backend proof sample

`proof-sample/` preserves the original tracked repository files from commit
`15d1ec6ce85f872e4547b37c33658dea76b25d03`, archived on 2026-10-03. Its backend,
tests, fixtures, documentation, SDK/package/tool pins, editor configuration, and historical
workflow are retained byte for byte. `proof-sample/SNAPSHOT.json` records the source commit,
original paths, byte counts, and SHA-256 hashes for all 800 tracked files.

Verify that source snapshot from the repository root with
`python3 tools/repository/verify-archive.py`. CI runs the same check.

This is historical evidence. Active policy lives in the root repository documents.
Historical AGENTS files, plans, ADRs, review registries, and workflows describe the proof
sample; they do not authorize work on the new libraries. The original documents remain
unchanged, including their historical status and commands.

## Build and Fast proofs

From the repository root:

```bash
cd archive/proof-sample
dotnet restore ModulithFoundry.slnx
dotnet tool restore
dotnet csharpier check . --include-generated --ignore-path .gitignore
dotnet format style ModulithFoundry.slnx --verify-no-changes --no-restore
dotnet format analyzers ModulithFoundry.slnx --verify-no-changes --no-restore
dotnet build ModulithFoundry.slnx --no-restore
dotnet test --project tests/ArchitectureTests/ArchitectureTests.csproj --no-build --no-restore
dotnet test --project tests/ApplicationTests/ApplicationTests.csproj --no-build --no-restore
```

The explicit formatter ignore path prevents the active root's archive exclusion from
suppressing the archived check. The archive has its own build/package defaults, SDK pin,
tool manifest, `.editorconfig`, solution, and relative project references.

## Container and local runtime proofs

From the same archived directory, after applying the original
[Docker/Podman setup](proof-sample/tests/README.md):

```bash
dotnet test --project tests/PersistenceTests/PersistenceTests.csproj --no-build --no-restore
dotnet test --project tests/BrokerTests/BrokerTests.csproj --no-build --no-restore
dotnet test --project tests/TopologyTests/TopologyTests.csproj --no-build --no-restore
```

For manual runtime use, run `aspire start`, `aspire wait`, and `aspire stop` from this directory
using the retained Aspire configuration. Supply the same parameter/user-secret configuration
as before; credentials, running infrastructure, volumes, and build outputs are not preserved
by the source manifest. Relocation does not certify offline restore or future compatibility
with uninstalled SDKs/packages/container images.

## Evidence entry points

- [Module ownership and collaboration](proof-sample/docs/modules/README.md)
- [Second event-sourced aggregate](proof-sample/docs/plans/second-event-sourced-aggregate.md)
- [Event-sourcing correctness and limits](proof-sample/docs/plans/event-sourcing-correctness-gate.md)
- [Messaging extraction comparison](proof-sample/docs/plans/messaging-reuse.md)
- [Local durability closure and remaining deployment gaps](proof-sample/docs/plans/durability-closure-checklist.md)

CI runs the relocated test lanes through the active root workflow. The workflow snapshot
inside this archive is reference material and is not discovered by GitHub Actions.
