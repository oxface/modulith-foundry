---
status: accepted
---

# Use module-owned messaging endpoints

Each module that actually consumes asynchronous messages owns one stable broker input queue, one module-specific error queue, and an isolated Rebus handler container created with `AddRebusService`. Directed integration commands use `Send` to the owning module queue; integration events use `Publish` through transport-specific fan-out infrastructure so each subscribing module receives a copy in its existing input queue. Commands and subscribed events share that module queue until measured throughput, latency, scaling, or failure-isolation requirements justify another endpoint.

The producer owns its transactional PostgreSQL outbox and publication responsibility, not an output queue or its consumers' subscriptions. This topology preserves module failure isolation and stable queue ownership for later extraction without pretending that synchronous calls or consumer state bootstrap become automatically distributable.
