# CI test output cleanup

Date: 2026-10-08. Base checkpoint: `ecab884`. Changes remain unstaged for review.

## Outcome and scope

All 24 direct workflow test commands now use native `dotnet test` options:

```text
--no-progress --no-ansi --output Normal
--show-stdout Failed --show-stderr Failed
```

The main workflow covers all family, Wholesale and architecture tests. The sample-runtime
workflow applies the same options to the Aspire/browser suite. Template verification passes
these options to both generated-consumer test invocations; its workflow still invokes the
same TypeScript proof. No new shared script or runtime/library mechanism was introduced.

Review-worthy files are `.github/workflows/ci.yml`, `.github/workflows/sample-runtime.yml`,
`tools/template/verify.ts` and this report. Test projects, filters, restore/build behavior,
workflow triggers, job names, timeouts and coverage remain unchanged. Local hooks and generated
application source are outside this logging-only change.

## Verification and limits

- Actionlint passed for all three workflows.
- Parsed before/after commands retained all 24 direct invocations and filters, with only the
  five logging options added.
- Template tooling TypeScript and Prettier checks passed.
- A current main-workflow command ran MessagingTests: 7 passed, summary output, no progress/ANSI.
- Whitespace checks passed.

Immediately before this change, the same installed runner executed InboxPostgresTests with
both logging configurations: all 28 passed in each run. Application-level `--progress off
--ansi off --output Detailed` produced 71 lines and three CLI progress updates. Native CLI
`--no-progress --no-ansi --output Normal` with failed-only captured output produced nine lines,
no progress updates and no ANSI. Those executions establish the selected flags; they are not
additional unique inbox correctness proofs.

Normal output retains the test summary and failure diagnostics; Detailed also lists passing
cases. Stdout/stderr policy applies to captured test output, not arbitrary uncaptured process
logs or restore/build output. Full template generation and the five-minute runtime suite were
not repeated for logging arguments alone. Hosted rendering awaits the next CI execution.
