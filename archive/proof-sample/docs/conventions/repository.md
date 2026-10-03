# Repository workflow

## Reviewable pull requests

Each pull request delivers one coherent outcome and leaves the repository in a valid state. It includes the implementation, tests, migrations, and documentation required for that outcome; it does not rely on an unreviewable follow-up to become correct.

Keep a pull request within a normal, single-reviewer scope: the repository owner should reasonably be able to understand the intent, inspect the important behavior, and verify the evidence in one review. There is no mechanical line-count limit. Split work when independent outcomes can be reviewed and validated separately, but do not create artificial layers, omit tests, or leave broken intermediate states merely to reduce diff size.

A v1 slice is a planning and acceptance unit, not necessarily one pull request. Deliver a large slice through several self-contained vertical increments when that improves reviewability.

During the remaining implementation slices, follow the temporary [pattern-focused review workflow](pattern-review.md) when classifying changes, staging files, and preparing an owner-review handoff.

## Commit approval gate

The repository owner personally approves every commit before it is created. Agents and other contributors may edit files, run checks, and prepare a proposed change set, but must not run `git commit` until the owner explicitly approves that exact change set.

Before requesting approval, present:

- the coherent outcome and important design choices;
- the files and behavior changed;
- tests and other verification performed; and
- any known risk, omission, or follow-up.

Plan approval, task authorization, successful checks, silence, or approval of an earlier change set is not commit approval. If the proposed files change after approval, present the updated change set and obtain approval again before committing.
