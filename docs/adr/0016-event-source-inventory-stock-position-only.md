---
status: accepted
---

# Event-source Inventory Stock Position only

The reference implementation event-sources one Inventory aggregate family, Stock Position, as a deliberate capability proof while other aggregates remain state-stored. Its module-owned PostgreSQL stream uses expected-version appends, immutable JSONB events with stable names and versions, one evolution entry point for replay and new events, inline atomic projections, recorded-time reconstruction, explicit correction events, and compatibility fixtures; snapshots, bitemporal history, a generic upcaster framework, and a shared event-store library are deferred until measured or repeated needs justify them.
