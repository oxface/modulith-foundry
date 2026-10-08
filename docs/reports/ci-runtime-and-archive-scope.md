# Active CI runtime trust and archive boundary

Date: 2026-10-08. Base checkpoint: `bce8c5b`.

## Reported failure and diagnosis

[CI run 37762865480](https://github.com/oxface/modulith-foundry/actions/runs/37762865480)
passed the active context and PostgreSQL lanes. The active Aspire runtime lane failed;
archived RabbitMQ and Topology lanes were cancelled after roughly twelve minutes.
Job status/timing was read from GitHub's public API. Detailed failure excerpts were supplied
by the owner because unauthenticated job-log downloads were unavailable.

The browser test expected an HTTPS Keycloak authority but received HTTP. The workflow only
created a development certificate. Aspire 13.5.4 selects trusted development certificates
for automatic HTTPS and outbound trust; creation alone does not supply either. Its native
API health check also selects the HTTPS endpoint. See the
[pinned certificate selection source](https://github.com/microsoft/aspire/blob/9c1b401dd67746739044f68959cbf4d3d7af93a6/src/Aspire.Hosting/DeveloperCertificateService.cs)
and [Linux trust requirements](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl?view=aspnetcore-10.0).

A local process with `ASPIRE_DEVELOPER_CERTIFICATE_DEFAULT_TRUST=false` reproduced the exact
HTTPS-versus-HTTP assertion in the real Alpha Chromium journey: one failure in 34 seconds.
The same journey with normal trust passed in 51 seconds. This probe did not modify the
machine's certificate store or weaken application TLS validation.

The owner also supplied an archived PostgreSQL-restart connection failure and an archived
API startup failure. Those are historical application/test-harness issues, not Rootbolt
failures. Initial local archived probes passed, demonstrating that a local green result
does not settle their CI portability. No archived runtime guarantee is claimed or repaired
by this change. Further archive maintenance was stopped after the owner proposed excluding
it from active tests.

## Concrete changes and ownership

| Files | Final behavior |
| --- | --- |
| `.github/workflows/ci.yml` | Install Linux NSS trust tools, trust the native development certificate, retain system roots alongside its OpenSSL directory through `GITHUB_ENV`, and require a successful trust check before starting resources. Keep all active suites. Replace archived Fast execution with bounded repository checks; remove archived PostgreSQL, broker and topology jobs. |
| `lefthook.yml` | Remove archived style, analyzer and architecture jobs. Retain active formatting, analyzers and consumer/library checks. |
| `README.md`, `docs/development.md` | Describe active CI coverage, explicit certificate trust and the archive boundary. |
| `archive/README.md` | Keep manual historical commands and provenance; explain that active gates verify checksums without executing the archive. This guide is outside the frozen snapshot. |
| This report | Separate observed CI failures, local reproductions, the final repair and historical evidence. |

All 800 frozen files, manifests, fixtures and historical workflows remain unchanged. No
test assertion, native backchannel certificate check or business/library behavior was
relaxed. Browser contexts retain their already documented local-certificate exception.
No library interfaces, dependencies, snapshots, generated template sources or schema changed.

## Verification and limits

The corrected Linux OpenSSL environment passes `dotnet dev-certs https --check --trust`.
The full active runtime suite passed all four cases, with zero failures/skips, in
2 minutes 17 seconds. It ran with two logical processors, real PostgreSQL, native
Aspire/Kestrel/Keycloak and Chromium, using the corrected OpenSSL environment.

Actionlint 1.7.12 validates both active workflows. Both workflows and Lefthook parse as YAML;
Lefthook validation, current commitlint, root CSharpier and diff whitespace checks pass.
All 132 local links in the changed documents resolve. The source snapshot verifier matches all
800 archived originals. No frozen archive file is in the change set. Existing active library
and persistence tests passed in the reported CI run and the rename checkpoint; they are
historical evidence here and were not rerun for these workflow/documentation-only edits.

The original source-trust failure remains detectable by the existing HTTPS assertion;
the explicit trust preflight also prevents spending test startup time on an untrusted runner.
A fresh hosted GitHub run still requires reviewing, committing and pushing this change.
Local verification does not claim that remote execution already passed.

**No new reusable mechanism was proven.** This increment repairs CI environment setup and
limits its coverage to the active libraries/application plus frozen-source integrity.
Archived tests remain available for deliberate manual investigation, with their historical
compatibility and recovery limitations. Their exclusion is explicit coverage policy, not
a claim that their reported failures were fixed.
