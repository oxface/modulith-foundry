# Test instructions

Follow the repository-wide instructions in [../AGENTS.md](../AGENTS.md) and the test strategy in [../docs/plans/architecture-and-delivery.md](../docs/plans/architecture-and-delivery.md).

- Put a test in the narrowest lane that proves the behavior. Do not replace PostgreSQL, broker, identity, or topology semantics with in-memory substitutes once those lanes exist.
- Architecture tests must cover both declared project/package references and compiled type dependencies. A rule that cannot detect an intentionally introduced violation is incomplete.
- Name tests `{OperationOrRule}_{Scenario}_{ExpectedOutcome}`. Use a concise two-part fact for an invariant with no meaningful scenario.
- Keep the Fast lane free of containers and personal credentials.
- Add every automated test to a documented CI path in the same increment that introduces it.
