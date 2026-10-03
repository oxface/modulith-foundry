---
status: accepted
---

# Start the event-sourcing proof with Inventory Stock Position

The first workflow event-sources one Inventory aggregate family, Stock Position, as a deliberate capability proof while other aggregates remain state-stored. Its module-owned PostgreSQL stream uses expected-version appends, immutable JSONB events with stable names and versions, one write-state evolution entry point for replay and new events, inline atomic projections, recorded-time reconstruction, explicit correction events, and compatibility fixtures; separate hydration-checkpoint snapshots, bitemporal history, and a generic upcaster framework remain deferred. Inline aggregate-shaped write models are recommended by default, and a second concrete event-sourced aggregate is explicitly scheduled in Increment 8.1, preferably in another module, before focused library extraction and final configured product scaffolding; this late validation avoids extracting Inventory assumptions as generic infrastructure.
