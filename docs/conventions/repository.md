# Repository workflow

## Reviewable increments

Deliver one coherent outcome per increment, including its implementation, consumer usage,
tests, and documentation. Keep the repository valid and the outcome understandable in one
owner review. Split independent outcomes rather than introducing empty layers or broken
intermediate states.

The owner approved archiving without line-by-line review of the relocation. Review focuses
on what remains at the root, new documentation, and any changed tooling or behavior. The
archive's checksum manifest distinguishes relocation from source changes.

Library interfaces and implementations receive line-by-line review. Sample usage and proof
changes accompany the library so the consumer obligations can be assessed together. A new
template file is needed only when the increment establishes a reusable setup pattern.

Leave all changes unstaged for owner review. The old pattern-focused automatic-staging
workflow is historical and no longer applies.

## Handoff

Present the coherent outcome, important choices, review-worthy files, verification, risks,
and remaining gaps. Each slice report also distinguishes library mechanisms from template
setup and sample policy, and states which guarantees were tested, rejected, proposed, or
inherited only as historical evidence. State explicitly when no new reusable mechanism
was proven.

## Commit approval

The repository owner must explicitly approve the exact complete change set before every
commit. Task authorization, plan approval, successful checks, silence, and earlier commit
approval do not authorize `git commit`.

Present the implementation and documentation changes, verification, and known limitations
before requesting commit approval. If the proposed files change after approval, obtain
approval for the updated change set. Include staged and unstaged work in the handoff.
