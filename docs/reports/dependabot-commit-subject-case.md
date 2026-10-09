# Dependabot commit subject capitalization

## Outcome

Disabled only `subject-case` in the active
[commitlint configuration](../../tools/repository/commitlint.config.mjs). Conventional
commit types, structure, nonempty subjects and the other inherited rules remain in
place for humans and bots. The [tooling guide](../../tools/repository/README.md)
documents the policy. Local hooks and CI already share this configuration.

The owner reported three failed dependency PRs. Public job step results showed
`Check pull-request commits` failing in all three, after restore, build, formatting,
analyzers and architecture verification succeeded. The bot subjects were:

- [#10](https://github.com/oxface/modulith-foundry/pull/10):
  `chore: Bump the aspire group with 3 updates`.
- [#11](https://github.com/oxface/modulith-foundry/pull/11):
  `chore: Bump the opentelemetry group with 6 updates`.
- [#12](https://github.com/oxface/modulith-foundry/pull/12):
  `chore: Bump Aspire.Hosting.AppHost`.

The capitalization rule alone rejects these valid conventional commits. Dependency
versions or runtime incompatibility were not the observed failure. No dependency
updates, bot PR commits, CI workflow definitions or archive files were changed.
No new reusable runtime mechanism was proven; this is repository validation policy.

## Verification

Native reproduction before the edit:

```sh
npm exec --prefix tools/repository -- commitlint \
  --config tools/repository/commitlint.config.mjs --verbose <<'COMMIT'
chore: Bump the aspire group with 3 updates
COMMIT
```

It exited 1 with only `subject-case`. The actual complete #11 commit message,
including dependency metadata and its signed-off footer, produced the same error.
This exact error and minimal header made instrumentation and speculative package
hypotheses unnecessary.

After the edit, native commitlint accepted the complete messages fetched for #10
`02eddb1`, #11 `3b19a2a` and #12 `5e3f4c4`, along with lowercase/scoped conventional
examples. Negative examples still rejected unknown types, missing structure and
empty subjects. Syntax and whitespace checks passed; the original empty index was
preserved. No new validator dependency or durable test harness was introduced.

The local result proves commit-message acceptance. Hosted CI must rerun after this
policy reaches the PR's tested revision. Rebase/update the affected bot branches
onto the merged policy; rerunning an unchanged old revision still uses its old
configuration. No remote comments, review, merge, closure, commit or push was made.

The prior hosted jobs' results are evidence for those specific revisions, not new
local runtime-test results. No C#, PostgreSQL, broker or browser suite was rerun for
this isolated rule change.
