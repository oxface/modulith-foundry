---
status: accepted
---

# Distinguish state-stored and event-sourced domain events

State-stored and event-sourced aggregates may share a small pending-domain-event abstraction but use separate aggregate bases. A state-stored aggregate persists its current relational state and its pending domain-event objects are ephemeral unless deliberately mapped to an audit, activity, projection, or outbox record. An event-sourced aggregate evolves historical and newly decided events through the same evolution entry point, but only new events enter its pending collection and durable stream; replayed events are never redispatched. The aggregate/repository determines persistence semantics, while integration events remain separately versioned public contracts. Persistence must not recursively discover and dispatch aggregate-changing events through EF Core `SaveChanges` or `ChangeTracker`; multi-aggregate coordination is explicit in the owning application use case or durable process.
