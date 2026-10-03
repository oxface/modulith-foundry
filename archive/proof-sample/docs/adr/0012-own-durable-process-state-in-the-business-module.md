---
status: accepted
---

# Own durable process state in the business module

The first long-running workflow is a concrete Sales-owned Order Fulfilment Process persisted through the Sales EF Core context. Normal Rebus handlers adapt broker messages to application handlers; the same module transaction commits inbox receipt, process state, Sales domain changes, deadlines, audit/activity, and outgoing outbox records. Shared infrastructure supplies reliability mechanics but not a generic saga DSL or orchestrator project. Rebus `Saga<TSagaData>` may be reconsidered by a focused spike only if its persistence can preserve an equally explicit atomicity and idempotency model.
