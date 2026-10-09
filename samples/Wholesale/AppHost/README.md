# Wholesale local runtime

This editable sample AppHost composes PostgreSQL 18.6, its `wholesale` database, the existing
HTTP API and a manually started `demo-setup` command. It requires the pinned Aspire CLI
13.6.1, .NET from root `global.json`, a Docker-compatible engine and a native HTTPS
development certificate. Root `aspire.config.json` selects this active AppHost rather than
the archived graph. Explicit `--apphost` paths also work.

```bash
aspire_version="$(DOTNET_NOLOGO=true dotnet msbuild samples/Wholesale/AppHost/Wholesale.AppHost.csproj -getProperty:AspireHostingSDKVersion -nologo)"
dotnet tool install --global Aspire.Cli --version "$aspire_version"
dotnet dev-certs https
dotnet restore ModulithFoundry.slnx
dotnet build ModulithFoundry.slnx --no-restore
```

If a matching CLI is already installed, use it. The
[native package](https://www.nuget.org/packages/Aspire.Cli/13.6.1) supports the pinned install
command. Native project-path references select the API without generated application types.
Build the solution before using `--no-build`; the AppHost itself has no API project reference.
Do not rebuild running resources; stop the graph first.

Keep the stable Aspire hosting/testing packages and AppHost SDK aligned. CI and the
install command above obtain the CLI version from the native `AspireHostingSDKVersion`
MSBuild property, so the workflow has no independent CLI version pin. Dependabot's
NuGet update does not update the SDK attribute; review it when upgrading Aspire. Version 13.6.1
includes the upstream fix for the one-minute DCP watch failure in 13.6.0; see the
[upgrade verification](../../../docs/reports/aspire-watch-timeout-upgrade.md).

## Configure

Supply these through normal AppHost configuration (user secrets, environment or your usual
secret provider). Environment variables use double underscores.

| AppHost key | Purpose |
| --- | --- |
| `Parameters:postgres-password` | Required secret database password. Keep stable when reusing the named volume. |
| `Parameters:oidc-authority` | Required HTTPS authority passed to the API's native OIDC configuration. |
| `Parameters:oidc-client-id` | Required authorization-code client identifier passed to the API. |
| `LocalDevelopment:UseDataVolume` | Defaults to true. False uses ephemeral PostgreSQL storage for a disposable graph. |
| `LocalDevelopment:UseLocalIdentityProvider` | Defaults to false. True adds the local HTTPS Keycloak realm instead of external authority/client parameters. |
| `LocalDevelopment:KeycloakPort` | Local provider port, default 58081. Keep stable with retained Access mappings. Isolated/test graphs randomize ports. |

For example, set secrets with `dotnet user-secrets set --project
samples/Wholesale/AppHost/Wholesale.AppHost.csproj KEY VALUE` using your values.
Do not commit credentials. For Podman set `ASPIRE_CONTAINER_RUNTIME=podman` and `DOCKER_HOST`
to your rootless socket. Aspire supplies the actual database connection as
`ConnectionStrings:Access` to API and setup; all three modules register their own contexts.

The provider-free runtime test uses inert `https://identity.test` settings solely for public requests.
For external authentication configure the API's
`Oidc:ClientSecret` if required, provision an exact external issuer/subject pair against an
application user and register the discovered HTTPS `/signin-oidc` callback. This graph does
not provision your external provider. See [HTTP configuration](../HttpIdentityDemo/README.md).

## Optional local identity provider

Set `LocalDevelopment:UseLocalIdentityProvider=true` and provide these secret AppHost
parameters through your usual configuration provider:

| Key | Purpose |
| --- | --- |
| `Parameters:postgres-password` | The existing database credential. |
| `Parameters:keycloak-password` | Local Keycloak administrator credential. |
| `Parameters:oidc-client-secret` | Local confidential `wholesale-bff` client secret. |
| `Parameters:demo-user-password` | Disposable password shared by `alpha`, `beta` and `unmapped` demo accounts. |

External `oidc-authority`/`oidc-client-id` parameters are not needed in this mode. The graph
uses Keycloak 26.8.0 and the explicitly pinned preview hosting integration, native HTTPS
certificate handling and an ephemeral realm import. Its exact browser callback uses
Aspire's localhost network context, so container endpoint translation cannot replace the
browser's hostname. There is no redirect wildcard or backchannel certificate bypass.

For normal local use, start without `--isolated` to retain the configured stable provider port:

```bash
aspire start --apphost samples/Wholesale/AppHost/Wholesale.AppHost.csproj --no-build --non-interactive
aspire wait keycloak --apphost samples/Wholesale/AppHost/Wholesale.AppHost.csproj --non-interactive --timeout 90
```

Skip the repeated start command below if this graph is already running. Continue with
explicit `demo-setup` on a fresh database and check its exit 0 before
using demo rows. Setup maps actual imported subjects and the allocated realm issuer to the
two existing application users. Visit the API's discovered HTTPS `/login`, sign in as
`alpha` or `beta`, and the native callback returns to `/identity`. Alpha belongs to both
Organizations; Beta belongs only to South. `unmapped` has no application link and receives
403 after provider login, even though its email matches Alpha. Registration is disabled;
requests never provision accounts or link email addresses.

The provider is ephemeral and reimports the exact callback each session. PostgreSQL data
may be retained. A stable issuer preserves its existing mappings; if the authority changes
(including its port), update application links explicitly rather than rerunning seed setup.
For a throwaway `--isolated` graph choose `UseDataVolume=false`, since its randomized issuer
must not silently change retained Access data. Browser tests use that fully disposable mode.
Trust the native development certificate for ordinary manual browsing. Automated browser
contexts explicitly ignore development certificate trust errors; this is not production
certificate-trust evidence.

## Start and explicitly initialize

Run from the repository root after configuration:

```bash
aspire start --apphost samples/Wholesale/AppHost/Wholesale.AppHost.csproj --no-build --non-interactive
aspire wait wholesale --apphost samples/Wholesale/AppHost/Wholesale.AppHost.csproj --non-interactive --timeout 90
aspire wait api --status up --apphost samples/Wholesale/AppHost/Wholesale.AppHost.csproj --non-interactive --timeout 90
aspire describe --apphost samples/Wholesale/AppHost/Wholesale.AppHost.csproj --non-interactive
```

`describe` supplies actual dynamic HTTP/HTTPS API and dashboard addresses; no fixed port is
assumed. On a fresh database `/health/live` is 200 and `/health/ready` is 503. Starting the
graph does not run `demo-setup`, apply module migrations or create demo rows. Native database
creation is separate from schema migration. For a fresh disposable database, explicitly run:

```bash
aspire resource demo-setup start --apphost samples/Wholesale/AppHost/Wholesale.AppHost.csproj --non-interactive
aspire wait api --apphost samples/Wholesale/AppHost/Wholesale.AppHost.csproj --non-interactive --timeout 90
aspire describe demo-setup --apphost samples/Wholesale/AppHost/Wholesale.AppHost.csproj --non-interactive
```

Check setup finished with exit 0. Readiness checks table presence and can become healthy
before seed work finishes; it is not a setup completion signal. The resource runs the existing finite `--initialize-demo` mode,
applies each module's own migrations/history and explicitly seeds its demonstration rows.
Then the public `/organizations/north-supply/catalog` and `/organizations/south-supply/catalog`
return 42 and 7 available units. Setup is not repeatable reconciliation; rerunning it against
populated demo rows fails. On retained storage the API may already be ready; don't run setup
just because a new AppHost session was started.

Default PostgreSQL storage is a retained named volume. Stopping the graph stops/removes its
session resources but preserves this volume; it does not drop module data. Changing a
configured password does not reset a previously initialized PostgreSQL volume. Select
ephemeral storage before starting a throwaway proof instead of deleting personal volumes.

## Health, telemetry and shutdown

`/health` remains the old liveness alias. `/health/live` checks only the process's self check;
`/health/ready` also checks database connectivity and the seven module tables used by the
sample. These anonymous/tenantless endpoints perform no writes. Readiness is not a migration
checksum, seeded-data, tenant-authorization or identity-provider check.

The host's [ServiceDefaults](../ServiceDefaults/README.md) registers native telemetry;
Aspire provides OTLP configuration. After sending a catalog request, inspect the dashboard
or use these commands (export delivery is asynchronous):

```bash
aspire otel traces api --apphost samples/Wholesale/AppHost/Wholesale.AppHost.csproj --non-interactive
aspire otel spans api --apphost samples/Wholesale/AppHost/Wholesale.AppHost.csproj --non-interactive
aspire otel logs api --apphost samples/Wholesale/AppHost/Wholesale.AppHost.csproj --non-interactive
aspire stop --apphost samples/Wholesale/AppHost/Wholesale.AppHost.csproj --non-interactive
```

Native telemetry preserves request/database trace relationships and correlates error logs.
Metrics are configured too; CLI 13.6.1 exposes traces/spans/logs, so use the dashboard for
metrics. The runtime test independently proves database-outage readiness 503, liveness 200
and failed business reads. There is no automatic repair, global HTTP retry policy or custom
shutdown worker. Local stop/cleanup was verified; exporter-outage delivery guarantees,
production deployment, external providers and proxy/subdomain sessions remain separate proofs.

See [runtime evidence](../../../docs/reports/e3-6-runtime-composition.md),
[OIDC/browser evidence](../../../docs/reports/e3-7-oidc-browser-journey.md) and
[test commands](../../../docs/development.md). The AppHost/health/ServiceDefaults source is
editable template material; generated template output and the bootstrap CLI remain E10.
