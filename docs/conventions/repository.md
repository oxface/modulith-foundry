# Repository workflow

## Reviewable increments

Deliver one coherent outcome per increment, including its implementation, consumer usage,
tests, and documentation. Keep the repository valid and the outcome understandable in one
owner review. Split independent outcomes rather than introducing empty layers or broken
intermediate states.

Library interfaces and implementations receive line-by-line review. Sample usage and proof
changes accompany the library so the consumer obligations can be assessed together. A new
template file is needed only when the increment establishes a reusable setup pattern.

## Repository scripts

Use TypeScript for new durable repository scripts and proof harnesses, and when actively
reworking such tooling. Document the runtime and invocation, and verify types and formatting.
Preserve archived tooling and unrelated existing scripts; this convention does not authorize
a repository-wide migration.

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

## Library documentation

Keep each library's consumer setup, current capabilities, limitations and deferred-feature
context beside its source, in its README and local docs directory where useful. A consumer
should not need the repository's root plans to discover an essential guarantee or integration
obligation. Keep related package composition and optional dependencies explicit.

Root design decisions, ADRs, interface proposals and slice reports retain cross-cutting review
and dated evidence; link them to the owning library's current capability record. Mark future
features as deferred rather than supported, including their known proof obligations. Moving
project/test folders is a separately reviewed relocation with references, template snapshots
and checks accounted for; documentation locality does not require moving everything at once.
