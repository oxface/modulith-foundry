# Native EF outbox composition

References Messaging and native EF Core Relational, DI, Hosting and Logging abstractions.
No Npgsql, broker, Events, EventSourcing, Persistence or Tenancy dependency.

`IOutbox<TDbContext>`/`EfOutbox<TDbContext>` enqueue a provided `OutboxMessageRecord` in the
caller's explicit native transaction. They do not save, commit, dispatch or open a producer
transaction. DbSet declarations are optional; use native Set<OutboxMessageRecord>().

`ConfigureOutbox(model,schema,table)` supplies the relational shape; provider configuration
must specialize payload/time storage. Use [ConfigurePostgresOutbox](../Rootbolt.Messaging.EntityFrameworkCore.Postgres/README.md)
for the currently supported runtime. No second provider is certified by this base mapping.

Call `ValidateOutboxChanges()` before base in both SaveChanges(bool) and
SaveChangesAsync(bool,CancellationToken) overrides. It checks that added rows came from enqueue
in the same native transaction and rejects envelope/lifecycle mutation or deletion through
tracked EF writes. Technical initial timestamps are database-generated. No base context or
automatic interceptor installs these obligations. Omitted guards, bulk/raw SQL and external
writers are outside this contract.

`IOutboxDispatcher<TDbContext>` supplies one separately callable dispatch operation.
NoWork means no eligible row was claimed at that moment; Published means acceptance plus
guarded completion; ClaimLost can follow successful acceptance and permits later delivery.

Optionally register `AddOutboxWorker<TDbContext>(new OutboxWorkerOptions(idleDelay,failureDelay))`
after a concrete dispatcher and native logging registration. It starts no migrations and
discovers no modules. Each sequential invocation gets a fresh async scope, disposed before
waiting. Dependency/model/provider failures during resolution fail startup/execution;
publication/storage failures are logged and retried with failureDelay. IdleDelay applies on
NoWork; both delays must be positive and within native Task.Delay bounds. Cooperative host
cancellation stops new work and reaches the publisher. A non-cooperative publisher can still
hold shutdown; leases do not forcibly terminate code. Callable dispatch requires no worker.

Keep domain eligibility, whole-operation retry, tenant admission, final save/commit and
deployment policy with the consumer. See [complete obligations](../docs/capabilities.md).
