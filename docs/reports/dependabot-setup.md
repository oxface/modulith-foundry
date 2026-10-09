# Dependabot setup

## Outcome and scope

Added repository-native Dependabot configuration on `chore/dependabot-setup`, branched
without an upstream from fetched `origin/main` at `0aa69de`. No dependency versions,
runtime code, migrations, template source snapshots or archived files changed.

Review-worthy files:

- [Configuration](../../.github/dependabot.yml): five update entries for NuGet, two npm
  tool directories, GitHub Actions and the .NET SDK.
- [Development guide](../development.md#dependency-updates): grouping/review policy,
  repository security settings and hosted verification obligations.
- This report: current evidence and remaining gaps.

Checks run Mondays at 09:00 UTC with three open routine PRs per configuration. NuGet,
npm and SDK routine releases wait seven days; majors wait 30. Actions uses 30 days for
all releases because per-SemVer cooldowns are unsupported for that ecosystem. Security
updates bypass cooldowns and have no configured groups. No automatic merge is added.

Routine groups follow EF/provider/tool, .NET platform, Aspire, OpenTelemetry,
Testcontainers, each npm tool directory and CI boundaries. Major updates are separate
from routine groups. Remaining NuGet packages use one dependency per PR across both
solutions; the SDK configuration pairs the two manifests for the same SDK upgrade.
Archive paths are excluded, and npm/SDK locations are explicit. There is no frontend
entry before a frontend exists.

## New verification

- YAML parsed with the existing tooling's `js-yaml` and validated with its existing
  Ajv against the downloaded SchemaStore Dependabot schema. Passed, including
  `dotnet-sdk`, multi-directory entries, cooldowns and `group-by: dependency-name`.
  No validator dependency or repository script was added.
- Inspected GitHub's official cooldown support table: NuGet, npm and SDK support
  per-SemVer delays; Actions supports only `default-days`.
- Inspected current upstream NuGet discovery and SDK fetcher source. NuGet starts
  from each workspace's direct solution/project entry points and expands referenced
  projects; the SDK fetcher reads that workspace's `global.json`. Both root and template
  directories are explicitly selected, since the template solution is independent.
  This is source inspection, not a hosted discovery execution.
- Documentation's local file links resolve, `git diff --check` passed, and the existing
  archive verifier matched all 800 original files. The index remains unchanged and empty.

Existing CI successes belong to earlier changes. No C#, database, broker or browser
suite was rerun for this configuration-only change. No new reusable runtime library
mechanism was proven; this is repository maintenance policy.

## Remaining verification and limitations

The configuration activates version updates after it reaches the default branch. The
first hosted Dependabot run must confirm discovery of both solutions/tools and SDK
manifests, expected grouping, preserved Actions SHA pins and compatibility with the
normal PR checks. Local schema validation does not prove GitHub scheduling or updates.

Dependency graph, alerts and security updates require repository security settings;
they are not enabled by this YAML. This workspace has no authenticated GitHub API
capability to verify or change them. The development guide records the manual setup,
including leaving broad grouped security updates disabled.

Cooldowns are release-age controls for update proposals, not universal installation
guards or safety certification. Groups do not prove compatible versions. Existing
preview dependencies and provider/framework combinations still require review.
Scripts' embedded CLI versions and container image strings are outside these initial
managers; review associated pins when their owning dependencies change. There is no
automatic SDK/package/embedded-tool synchronization guarantee.

The new `.github` configuration selects full coverage under current CI path rules.
Routine dependency PRs subsequently follow those same rules. Frozen archive checksum
verification remains in place. Changes are unstaged; no commit or push was performed.

## Follow-up: Node typings runtime boundary

The first hosted PRs demonstrated initial bot activity: [SDK #7](https://github.com/oxface/modulith-foundry/pull/7)
updates both SDK manifests, [typings #8](https://github.com/oxface/modulith-foundry/pull/8)
proposes Node 26 types, and [Lefthook #9](https://github.com/oxface/modulith-foundry/pull/9)
updates the hook tool and its platform packages. This does not yet prove the other
ecosystems' discovery or future security-update behavior.

Added an npm version-range exclusion for `@types/node >=25.0.0` in `/tools/template`,
matching its existing Node 24 engines and CI runtime. Eligible 24.x minor/patch updates
retain the existing group and cooldown. Other packages keep their major-update policy.
Version exclusions also apply to security-update candidates; an out-of-range fix needs
an explicit runtime upgrade decision. The boundary must be revisited with that upgrade.
No package versions, lockfiles or CI paths were changed in this follow-up.

Fresh public API results showed all 14 checks successful for current #7 revision
`3e810e8` and #9 revision `2d5421e`. On #8 revision `1fb14a3`, five checks succeeded
and nine were skipped, demonstrating template-only job selection despite 14 visible
results. Those hosted results belong to the bot PRs, not this local configuration edit.
This follow-up's YAML/schema, npm range and whitespace validation passed; the existing
index remains unchanged. No commit, push, remote review, merge or closure was performed.

## References

- [GitHub Dependabot options](https://docs.github.com/en/code-security/reference/supply-chain-security/dependabot-options-reference)
- [GitHub security-update setup](https://docs.github.com/en/code-security/how-tos/secure-your-supply-chain/secure-your-dependencies/configure-security-updates)
- [SchemaStore schema](https://github.com/SchemaStore/schemastore/blob/master/src/schemas/json/dependabot-2.0.json)
- [NuGet discovery source](https://github.com/dependabot/dependabot-core/blob/main/nuget/helpers/lib/NuGetUpdater/NuGetUpdater.Core/Discover/DiscoveryWorker.cs)
- [.NET SDK fetcher](https://github.com/dependabot/dependabot-core/blob/main/dotnet_sdk/lib/dependabot/dotnet_sdk/file_fetcher.rb)
