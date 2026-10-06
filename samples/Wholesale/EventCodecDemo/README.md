# Standalone two-family event codec consumer

A finite .NET 10 consumer of [Events.Serialization](../../../src/ModulithFoundry.Events.Serialization/README.md).
Its only project reference is the codec library; it has no package, database, HTTP, bus,
DI or context-library dependency. It demonstrates explicit event registration and native
JSON contracts without adopting the full modular-monolith application.

```bash
dotnet run --project samples/Wholesale/EventCodecDemo/EventCodecDemo.csproj
```

Expected output, independent of current numeric culture:

```text
inventory: item=11111111-1111-1111-1111-111111111111, location=22222222-2222-2222-2222-222222222222, unit=EA, on-hand=10.125
purchasing: code=OLD-1, supplier=SUP-1, currency=EUR, total=31.25
```

## Ownership and recipe

[Inventory](Inventory/StockPositionExample.cs) owns the stock-position example, item/location
identifiers, base unit, quantity interpretation and optional delivery reference. It explicitly
registers opened/received aliases and uses native `JsonRequired` plus nullable enforcement.
`StockPositionReceived` has a different CLR name from archived `StockReceived`; the retained
v1 alias/payload still reads correctly. The new optional delivery reference defaults to null
when absent. That compatibility choice belongs to Inventory, not the codec.

[Purchasing](Purchasing/PurchaseOrderExample.cs) owns draft/line payloads, supplier reference,
currency and line totals. It explicitly registers its two aliases and enables native required
constructor parameters and nullable enforcement. Its local fold replaces a line by exact
item code and calculates quantity times unit price. It does not depend on Inventory's events
or registry.

[FixtureEvents](FixtureEvents.cs) visibly reads/parses local fixture files and extracts the
envelope before invoking the codec. [DemoJourneys](DemoJourneys.cs) chooses exact fixture
paths/order, invokes the two consumer-local folds and prints their business results. There
is no automatic event discovery, required domain base class or registration framework.
See [fixture provenance](Fixtures/README.md) for copied versus newly authored literals.

These internal families exercise the library before E5 integrates event-sourced behavior
into the owning modules. They are not additional populated HTTP modules or empty Contracts
projects, and do not change the existing state-stored availability/profile sample. The folds
make decoded fields observable; they are not complete aggregates, sequence/history validators,
pending-event collectors, append operations or reservation/order workflows. No business-rule
completeness, temporal ordering, replay-effect, persistence or transaction guarantee is implied.

The exercised template material is this editable explicit registration/native JSON recipe.
Generated template output remains E10. Sample rules and module Contracts stay outside the
technical library.

## Proofs

```bash
dotnet test --project tests/EventSerializationTests/EventSerializationTests.csproj
dotnet test --project samples/Wholesale/EventCodecDemo.Tests/EventCodecDemo.Tests.csproj
```

The [consumer proofs](../EventCodecDemo.Tests/CompatibilityTests.cs) read actual local literals
and assert independently expected stock identity/quantity and purchase-order fields/totals.
They retain Inventory's required/null rules, Purchasing's constructor-field requirement and
an optional absent delivery reference. One family rejects the other's unregistered alias;
the actual console recipe exercises both. Library proofs cover conflicts, runtime dispatch,
unknown/unsupported identities, payload failures and configuration snapshots. They do not
repeat every native JSON option or test event-store behavior before it exists.

Both suites and the console run in container-free CI; focused suites also run in commit hooks.
See [the E4 report](../../../docs/reports/e4-event-serialization.md) for fresh verification and
remaining limits. No active build/runtime reference points to the archive.
