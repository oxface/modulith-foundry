# Modulith Foundry

A concrete .NET modular-monolith reference product used to prove architecture, delivery, deployment, and eventual copy/rename into a separate real product. It is not a reusable application framework.

Implementation follows the accepted architecture and delivery plan in review-sized increments.

- [Architecture and delivery plan](docs/plans/architecture-and-delivery.md)
- [V1 scope and deferred register](docs/plans/v1-scope.md)
- [V1 delivery slices](docs/plans/v1-slices.md)
- [Module charters](docs/modules/README.md)
- [Domain glossary](docs/domain/CONTEXT.md)
- [Architecture decisions](docs/adr/)
- [Primary-source research](docs/research/)
- [Repository workflow and approval gate](docs/conventions/repository.md)

## Development

The repository requires the SDK selected in `global.json`. The container-free Fast lane is:

```bash
dotnet restore ModulithFoundry.slnx
dotnet tool restore
dotnet csharpier check . --include-generated
dotnet format style ModulithFoundry.slnx --verify-no-changes --no-restore
dotnet format analyzers ModulithFoundry.slnx --verify-no-changes --no-restore
dotnet build ModulithFoundry.slnx --no-restore
dotnet test --project tests/ArchitectureTests/ArchitectureTests.csproj --no-build --no-restore
```

PostgreSQL and whole-topology behavior run in separate container-backed lanes documented in [tests/README.md](tests/README.md).

CSharpier is the repository-local C#/XML layout formatter. Run `dotnet csharpier format . --include-generated` to format the repository, including checked-in EF migration code; git-ignored build output is excluded. Settings live in `.editorconfig`: 100-column target width, four-space C#, two-space XML, and LF endings. The VS Code workspace recommends its official extension and enables format-on-save for C#/XML. Other editors can use the [official integrations](https://csharpier.com/docs/Editors). Keep `dotnet format` restricted to its `style` and `analyzers` commands; do not run its whitespace formatter over CSharpier output.

Repository-only Node tooling is isolated under `tools/repository`; it does not create the deferred frontend workspace. Install the pinned Conventional Commit and Lefthook tooling, then install hooks with:

```bash
npm ci --prefix tools/repository
npm exec --prefix tools/repository -- lefthook install
```

See the [application layout](apps/README.md), [module map](docs/modules/README.md), and [test layout](tests/README.md) for the executable runtime.
