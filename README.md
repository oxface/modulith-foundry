# Modulith Foundry

A reusable template and a set of opt-in .NET libraries for modular monoliths, developed
against an executable sample. Consumers own their application composition, module policy,
transactions, transport routing, and worker deployment.

The original wholesale sample and its documentation are preserved under
[`archive/proof-sample`](archive/proof-sample/). They supply behavioral evidence and known
limits for deliberate reimplementation, rather than prescribing the new library design.
The first active segments are independent [actor identity](src/ModulithFoundry.ActorIdentity/README.md)
and [tenancy](src/ModulithFoundry.Tenancy/README.md), exercised by [the finite wholesale context sample](samples/Wholesale/ContextDemo/README.md).
The first [EF ownership utility](src/ModulithFoundry.Persistence.EntityFrameworkCore/README.md)
was checkpointed with an executable Inventory consumer. The
[two-module persistence sample](samples/Wholesale/PersistenceDemo/README.md) now owns native
Inventory/Sales migrations and separate histories checkpointed as `f2dcf2b`.
Same-tenant customer/address relationships were checkpointed as `d67c7fc`.
Versioned profile changes with caller-owned transactions were checkpointed as `6069c05`,
completing the initial E2 persistence scope. The optional
[actor HTTP adapter](src/ModulithFoundry.ActorIdentity.AspNetCore/README.md) and
[HTTP identity sample](samples/Wholesale/HttpIdentityDemo/README.md) are implemented for E3.1
review. Tenancy admission and messaging follow later increments.

- [Current design decisions](docs/design.md)
- [Project glossary](CONTEXT.md)
- [Library and sample extraction plan](docs/plans/library-extraction.md)
- [E1 tenant/actor slice proposal](docs/plans/e1-tenant-actor.md)
- [E1 split implementation and proof report](docs/reports/e1-identity-split.md)
- [Original E1 checkpoint report](docs/reports/e1-tenant-actor.md)
- [E2 first-increment plan](docs/plans/e2-1-tenant-ownership.md)
- [E2.1 implementation and proof report](docs/reports/e2-1-tenant-ownership.md)
- [E2.2 module migrations plan](docs/plans/e2-2-module-migrations.md)
- [E2.2 implementation and proof report](docs/reports/e2-2-module-migrations.md)
- [E2.3 tenant relationships plan](docs/plans/e2-3-tenant-relationships.md)
- [E2.3 implementation and proof report](docs/reports/e2-3-tenant-relationships.md)
- [E2.4 versioned profile changes plan](docs/plans/e2-4-versioned-profile-changes.md)
- [E2.4 implementation and proof report](docs/reports/e2-4-versioned-profile-changes.md)
- [E3.1 actor HTTP integration proposal](docs/plans/e3-1-http-actor-identity.md)
- [E3.1 implementation and proof report](docs/reports/e3-1-http-actor-identity.md)
- [E2 persistence proposal](docs/plans/e2-persistence.md)
- [E2 design findings](docs/reports/e2-persistence-design.md)
- [Active .NET architecture checks](docs/reports/architecture-tests.md)
- [Active test value audit](docs/reports/test-audit.md)
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

CI checks active architecture, context, HTTP identity and EF model tests, with a separate active PostgreSQL ownership
lane. Archived Fast, PostgreSQL, RabbitMQ and Aspire Topology proofs remain independent.
