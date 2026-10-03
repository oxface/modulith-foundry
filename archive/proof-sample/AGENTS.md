# Repository instructions

## Change delivery

Before choosing change scope, preparing a pull request, or committing, read and follow [docs/conventions/repository.md](docs/conventions/repository.md).

Every commit requires the repository owner's explicit approval of the exact change set. Editing, testing, plan approval, or approval of an earlier commit does not authorize `git commit`.

## Slice reports

Each slice/iteration report includes template/library findings: what was proven or rejected, supporting evidence, extraction candidates versus module-owned policy, and remaining gaps. Distinguish tested guarantees from proposals; explicitly state when no new reusable mechanism was proven.

## Temporary pattern-focused review

This project's sample domain demonstrates modular-monolith capabilities; domain feature completeness is not the goal. During the remaining implementation slices, automatically stage routine reuse of approved patterns and leave review-worthy or mixed files wholly unstaged. Before classifying or staging changes, read [docs/conventions/pattern-review.md](docs/conventions/pattern-review.md) for the registry, mandatory review boundaries, and handoff requirements. This workflow ends when library extraction and sample creation begin.

## Durable context

- Read [docs/plans/architecture-and-delivery.md](docs/plans/architecture-and-delivery.md) before architecture or dependency work.
- Read [docs/plans/v1-scope.md](docs/plans/v1-scope.md) before changing v1 scope.
- Read [docs/plans/v1-slices.md](docs/plans/v1-slices.md) before implementation planning or choosing a pull-request increment.
- Read [docs/modules/README.md](docs/modules/README.md) and the owning module charter before changing module responsibilities or contracts.
- Read [docs/domain/CONTEXT.md](docs/domain/CONTEXT.md) before changing domain language or behavior.
- Record hard-to-reverse architecture decisions in `docs/adr/` only when the choice, rationale, and trade-off are settled.
