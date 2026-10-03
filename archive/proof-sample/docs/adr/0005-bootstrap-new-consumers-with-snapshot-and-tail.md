---
status: accepted
---

# Bootstrap new consumers with a snapshot and event tail

A newly added module or extracted service will not depend on replaying another module's private domain-event history. The producer supplies a versioned current-state export with a high-watermark cursor, after which the consumer idempotently follows versioned integration events from that cursor; this works for event-sourced and state-stored producers while preserving module ownership and event-contract evolution.

