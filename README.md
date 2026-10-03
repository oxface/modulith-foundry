# Modulith Foundry

A reusable template and a set of opt-in .NET libraries for modular monoliths, developed
against an executable sample. Consumers own their application composition, module policy,
transactions, transport routing, and worker deployment.

The original wholesale sample and its documentation are preserved under
[`archive/proof-sample`](archive/proof-sample/). They supply behavioral evidence and known
limits for deliberate reimplementation, rather than prescribing the new library design.
The new libraries and sample have not been implemented yet.

- [Current design decisions](docs/design.md)
- [Library and sample extraction plan](docs/plans/library-extraction.md)
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

CI continues to run the archived Fast, PostgreSQL, RabbitMQ, and Aspire Topology proofs.
New library and sample checks will join CI in the increment that introduces them.
