# Independent shared event-storage consumer

This small native EF executable uses only the EventSourcing.EntityFrameworkCore Foundry segment.
It has no modules, tenancy, actor, codec/history library, DI container or messaging dependency.
Counter/note facts are finite standalone proof data, not new Wholesale business rules.

```sh
export EVENT_STORAGE_DEMO_CONNECTION_STRING='<disposable PostgreSQL connection string>'
dotnet run --project samples/EventStorageDemo/EventStorageDemo.csproj
```

[StorageDbContext](StorageDbContext.cs) selects custom journal.streams/journal.facts tables,
maps jsonb through native EF and adds a consumer-defined Description column to its own header
row. [Rows](StorageRows.cs) implement the two library interfaces without a base class or tenant
field. The native design-time factory and migrations remain editable consumer source.

[DemoJourneys](DemoJourneys.cs) explicitly migrates, starts a native transaction, stages the
authored header/envelope rows, saves, commits or rolls back and disposes. Fresh-context queries
select counter/note stream types from the same pair of tables. Expected output:

```text
journal proof.counter: version=2, value=17
journal proof.note: version=1, text=review
```

The same GUID cannot create a second stream under another StreamType: Id is the chosen
identity. StreamType does not participate in the key. Positions are unique within a stream,
events reference an existing header and deleting a referenced header is restricted.

Setup writes only if the store is empty, for an ordinary sequential rerun of this finite demo.
Use a disposable database. It provides no concurrent bootstrap, generic replay, command API,
safe retry or ambiguous-commit recovery. Payload authoring and the demonstration calculation
are consumer code. Native mapping alone does not enforce the E5.2.2 append protocol.

```sh
dotnet test --project samples/EventStorageDemo.Tests/EventStorageDemo.Tests.csproj --no-restore
```

Tests use disposable PostgreSQL 18.6 Testcontainers and actual native migrations. They launch
this executable, verify mixed-type storage/customization, physical identity/relationship
boundaries and a stale native header update. Four focused model cases exercise safe key-shape
rejection and acceptance of differently named ownership prefixes; they require no connection.
The existing Wholesale suite proves complete append behavior after adopting the same utility.

[Library](../../src/ModulithFoundry.EventSourcing.EntityFrameworkCore/README.md),
[plan](../../docs/plans/e5-3-event-storage-registration.md),
[findings](../../docs/reports/e5-3-event-storage-registration.md).
