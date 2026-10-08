# Messaging family

Explicit transactional outbox storage, PostgreSQL recoverable dispatch and an optional
sequential worker. Transport configuration and business contracts belong to the consumer.

| Package | Responsibility |
| --- | --- |
| [Rootbolt.Messaging](Rootbolt.Messaging/README.md) | Provider-free immutable outgoing envelope and publication interface. BCL JSON only. |
| [Rootbolt.Messaging.EntityFrameworkCore](Rootbolt.Messaging.EntityFrameworkCore/README.md) | Provided record, relational mapping/enqueue/save guards, typed contracts and opt-in worker. |
| [Rootbolt.Messaging.EntityFrameworkCore.Postgres](Rootbolt.Messaging.EntityFrameworkCore.Postgres/README.md) | JSONB/database-time model specialization and PostgreSQL claim/completion/retry. |

Each module owns its context, schema/table, publisher and registrations. The family does
not supply a global bus, shared DbContext or cross-module transaction. Core publisher code
does not need EF; EF code does not reference Npgsql; PostgreSQL implements its native protocol.
Only PostgreSQL dispatch is supported. No generic provider dialect is introduced.

Start with [setup, guarantees and deferred capabilities](docs/capabilities.md). Executable
adopters are [Inventory/RabbitMQ](../../samples/Wholesale/EventPersistenceDemo/README.md)
and the [independent state-stored HTTP consumer](../../samples/OutboxDemo/README.md).

Tests run from the repository root:

```bash
dotnet test --project src/Rootbolt.Messaging/tests/MessagingTests/MessagingTests.csproj
dotnet test --project src/Rootbolt.Messaging/tests/OutboxPostgresTests/OutboxPostgresTests.csproj
dotnet test --project samples/OutboxDemo.Tests/OutboxDemo.Tests.csproj
```

PostgreSQL tests require a Docker-compatible runtime; Podman works via DOCKER_HOST pointing
to its user socket. Focused Inventory broker proofs additionally require RabbitMQ containers.
Architecture tests remain repository-owned. [The O1 report](../../docs/reports/outbox1-transactional-dispatch.md)
separates new results from archived evidence and proposed inbox work.
