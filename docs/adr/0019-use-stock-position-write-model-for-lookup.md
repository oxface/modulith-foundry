---
status: accepted
---

# Use the Stock Position write model for identity lookup

Stock Position resolves its organization/location/item business key through its required, atomically maintained inline write model and enforces uniqueness there; the event-stream header remains domain-neutral. This avoids a second identity index and loading the entire history for every command, while retaining explicit live reconstruction for history and verification. The trade-off is operational: retirement must retain lookup identity and projection loss requires repair with affected writes disabled; out-of-band row deletion followed by an expected-version-zero creation is not independently prevented. JSONB write-model storage is a separate, unproven proposal, not part of this decision.
