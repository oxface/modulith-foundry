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

The repository requires the SDK selected in `global.json`. The current container-free Fast lane is:

```bash
dotnet restore ModulithFoundry.slnx
dotnet format ModulithFoundry.slnx --verify-no-changes --no-restore
dotnet build ModulithFoundry.slnx --no-restore
dotnet test --solution ModulithFoundry.slnx --no-build --no-restore
```

Repository-only Node tooling is isolated under `tools/repository`; it does not create the deferred frontend workspace. Install the pinned Conventional Commit and Lefthook tooling, then install hooks with:

```bash
npm ci --prefix tools/repository
npm exec --prefix tools/repository -- lefthook install
```

See the [application layout](apps/README.md), [module map](docs/modules/README.md), and [test layout](tests/README.md) for the executable skeleton.
