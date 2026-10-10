# Messaging family

Explicit transactional outbox, retained inbox intake and local processing, with independently
selected sequential workers. Transport configuration and business contracts belong to the consumer.

| Package | Responsibility |
| --- | --- |
| [Rootbolt.Messaging](Rootbolt.Messaging/README.md) | Provider-free incoming/outgoing envelopes and publication interface. BCL JSON only. |
| [Rootbolt.Messaging.EntityFrameworkCore](Rootbolt.Messaging.EntityFrameworkCore/README.md) | Provided records, mapping/save guards, typed roles, keyed handlers and opt-in workers. |
| [Rootbolt.Messaging.EntityFrameworkCore.Postgres](Rootbolt.Messaging.EntityFrameworkCore.Postgres/README.md) | JSONB/database-time mapping, outbox leases, inbox deduplication and locked transactional processing. |

Each module owns its context, schema/table, publisher and registrations. The family does
not supply a global bus, shared DbContext or cross-module transaction. Core publisher code
does not need EF; EF code does not reference Npgsql; PostgreSQL implements its native protocol.
Only PostgreSQL dispatch/intake/processing is supported. No generic provider dialect is introduced.

Start with [setup, guarantees and deferred capabilities](docs/capabilities.md). Executable
adopters are [Inventory/RabbitMQ](../../samples/Wholesale/EventPersistenceDemo/README.md)
and the [independent state-stored HTTP sender](../../samples/OutboxDemo/README.md),
[inbox-only receiver](../../samples/InboxDemo/README.md) and
[two-module RabbitMQ journey](../../samples/MessagingDemo/README.md), followed by
[separate API/worker hosts](../../samples/MessagingWorkerDemo/README.md).
See [inbox setup and transaction ownership](docs/inbox.md).

Tests run from the repository root:

```bash
dotnet test --project src/Rootbolt.Messaging/tests/MessagingTests/MessagingTests.csproj
dotnet test --project src/Rootbolt.Messaging/tests/OutboxPostgresTests/OutboxPostgresTests.csproj
dotnet test --project src/Rootbolt.Messaging/tests/InboxPostgresTests/InboxPostgresTests.csproj
dotnet test --project samples/OutboxDemo.Tests/OutboxDemo.Tests.csproj
dotnet test --project samples/InboxDemo.Tests/InboxDemo.Tests.csproj
dotnet test --project samples/MessagingWorkerDemo.Tests/MessagingWorkerDemo.Tests.csproj
```

PostgreSQL tests require a Docker-compatible runtime; Podman works via DOCKER_HOST pointing
to its user socket. Focused Inventory broker proofs additionally require RabbitMQ containers.
The inbox PostgreSQL suite shares one assembly-level container, creates a fresh database
for each test and runs at most two tests concurrently. The outbox suite retains its sequential
collection fixture; its measured parallel candidates did not show a reliable speed improvement.
Architecture tests remain repository-owned. [The O1 report](../../docs/reports/outbox1-transactional-dispatch.md)
separates its results from archived evidence; [I1's report](../../docs/reports/inbox1-durable-intake-processing.md)
records the new inbox/adoption proofs separately.
