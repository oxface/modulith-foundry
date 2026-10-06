# Event codec literal fixtures

Inventory `opened.v1.json` and `received.v1.json` are byte-for-byte copies of the frozen
[`StockPositionEvents` fixtures](../../../../archive/proof-sample/tests/ApplicationTests/Fixtures/StockPositionEvents)
at original checkpoint `15d1ec6`. Their durable aliases, identifiers, field names and decimal
quantity are retained. The active build/runtime uses only these local copies.

Purchasing `drafted.v1.json` and `line-set.v1.json` are new literal fixtures matching the
archived [draft/line event contract](../../../../archive/proof-sample/modules/Purchasing/Purchasing/PurchaseOrders/Events/PurchaseOrderEvents.cs).
The draft values also appear in the archived unknown-schema/missing-field persistence proof;
there were no corresponding retained Purchasing JSON fixture files to relocate. The line
values deliberately give quantity 2.5, unit price 12.5 and independently expected total 31.25.

The envelopes contain only event name, schema version and payload. Stream identity/version,
timestamps, attribution, ordering validation and storage are outside this codec proof.
