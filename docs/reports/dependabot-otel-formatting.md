# OpenTelemetry dependency PR formatting repair

Date: 2026-10-09.

## Outcome and scope

Reviewed [PR #11](https://github.com/oxface/modulith-foundry/pull/11) at
`61fa1e4f3e325c81dc9543f27780f34e55e0f6bf` in a separate worktree. Its failing
Repository integrity step is **Verify root formatting**, not an integration test.
The same revision passed Wholesale runtime, consumer composition, library-family
checks and template adoption in GitHub Actions.

The initial repair formatted the `OpenTelemetry.Exporter.OpenTelemetryProtocol` reference
that Dependabot added to `samples/Wholesale/AppHost/Wholesale.AppHost.csproj`.
It retained the update's package versions and explicit version overrides. The owner
then requested consolidation with the Aspire repair. The final AppHost references
use central package versions instead of repeating `VersionOverride="1.19.1"`, so
the references fit the formatter's normal layout and future package updates have
one version source. The effective OpenTelemetry versions match the PR proposal.
No library interface, test assertion or formatting policy changes.

## Initial formatting verification

With .NET SDK 10.0.401 and the repository's pinned CSharpier 1.3.0:

```bash
dotnet tool restore
dotnet csharpier check . --include-generated
dotnet csharpier format samples/Wholesale/AppHost/Wholesale.AppHost.csproj
dotnet csharpier check . --include-generated
```

The first check rejected the long exporter reference. After formatting that file,
the root check passed all 528 files. `git diff --check` passed and the index stayed
empty. Existing runtime checks were not repeated for this whitespace-only repair;
their passing result is hosted evidence for the named PR revision, not a new local
runtime proof.

## Review and integration

The initial repair was isolated on `fix/otel-update-format`, based on the exact
Dependabot head. The owner-requested combined change set is now unstaged on
`fix/dependency-update-integration` in the main repository worktree. It includes
the six OpenTelemetry updates and both explicit AppHost references. See
[the combined verification](dependency-update-integration.md) for current evidence
and integration steps. There was no commit, push or remote PR modification.

No new reusable mechanism was proven. The sample owns its dependency composition;
formatting remains a repository check. The frozen archive was unchanged.
