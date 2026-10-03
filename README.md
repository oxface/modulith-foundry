# Modulith Foundry

A reusable template and a set of opt-in .NET libraries for modular monoliths, developed
against an executable sample. Consumers own their application composition, module policy,
transactions, transport routing, and worker deployment.

The original wholesale sample and its documentation are preserved under
[`archive/proof-sample`](archive/proof-sample/). They supply behavioral evidence and known
limits for deliberate reimplementation, rather than prescribing the new library design.
The first active segments are independent [actor identity](src/ModulithFoundry.ActorIdentity/README.md)
and [tenancy](src/ModulithFoundry.Tenancy/README.md), exercised by [the finite wholesale context sample](samples/Wholesale/ContextDemo/README.md).
Persistence, trusted HTTP ingress and messaging follow later extraction increments.

- [Current design decisions](docs/design.md)
- [Project glossary](CONTEXT.md)
- [Library and sample extraction plan](docs/plans/library-extraction.md)
- [E1 tenant/actor slice proposal](docs/plans/e1-tenant-actor.md)
- [E1 split implementation and proof report](docs/reports/e1-identity-split.md)
- [Original E1 checkpoint report](docs/reports/e1-tenant-actor.md)
- [E2 persistence proposal](docs/plans/e2-persistence.md)
- [E2 design findings](docs/reports/e2-persistence-design.md)
- [Development and verification](docs/development.md)
- [Repository workflow](docs/conventions/repository.md)
- [Archive provenance and commands](archive/README.md)

## Development

The SDK, formatter, editor settings, Lefthook, and Conventional Commit tooling remain
pinned in the repository. Install the existing repository tools with:

```bash
dotnet tool restore
npm ci --prefix tools/repository
npm exec --prefix tools/repository -- lefthook install
```

Run the active sample with:

```bash
dotnet run --project samples/Wholesale/ContextDemo/ContextDemo.csproj
```

CI checks the active actor/tenancy libraries and sample separately from archived Fast, PostgreSQL,
RabbitMQ and Aspire Topology proofs.
