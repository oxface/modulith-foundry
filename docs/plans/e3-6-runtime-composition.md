# E3.6 Runnable sample composition and native telemetry

Status: owner approved the slice direction after E3.4/E3.5 checkpoint `31c7a8b`.
Implementation and wiring remain for owner review; no commit is authorized.

## Outcome and resource ownership

Add a populated C# AppHost and a sample-owned ServiceDefaults project under
`samples/Wholesale`. PostgreSQL 18.6, its Wholesale database, the existing HTTP API and an
explicitly started `demo-setup` resource form the graph. The setup resource runs the existing
`--initialize-demo` command; there is no new migration facade, automatic startup seed or
additional Migrator wrapper. Each module keeps its existing schema/history/native migrations.

Use pinned Aspire 13.5.4, matching the installed CLI and the historical reference, while
checking actual APIs rather than copying archived infrastructure. Keep archived sources
unchanged. Native hosting project metadata is tooling; no library adds runtime code generation.
Use the native project-path API for the active graph, without generated application handlers.

PostgreSQL defaults to a named data volume with an explicitly configured secret password.
`LocalDevelopment:UseDataVolume=false` selects disposable storage for proofs. Its
container belongs to the AppHost session, so stopping the graph releases its resources;
data remains in the volume. API/setup receive the database through native WithReference
using the existing `ConnectionStrings:Access` name and wait for database readiness. Aspire
owns dynamic endpoint allocation. The API exposes HTTPS; no fixed callback port is selected
before the actual OIDC topology slice.

OIDC authority/client configuration stays explicit and external. E3.6 tests provide inert
configuration values and exercise only public requests; they do not replace authentication,
contact a fictitious provider, or certify login/session behavior. E3.7 owns the disposable
provider, real browser callback, mapped actor and protected mutation journey.

The API starts without awaiting manual setup. A fresh database is deliberately unready
until setup completes. Starting the AppHost never implicitly applies module migrations or
reconciles seed rows. The existing demo setup remains for a fresh disposable database only;
repeating it on populated rows fails rather than overwriting membership/data. Database
creation by the native PostgreSQL resource is separate from application schema migration.

## Health and telemetry

Keep `/health` as the existing liveness alias. Add `/health/live` for the native self check
and `/health/ready` for the native self check plus a host-owned database check. Health endpoints
explicitly permit anonymous/tenantless work. Readiness requires database connectivity and
all tables used by Access, Inventory and Sales; it does not inspect tenant business rows,
validate every migration checksum or certify identity-provider availability. No health check
saves/migrates/repairs. Native bounded health-check cancellation applies to the SQL query.

ServiceDefaults configures native ILogger, Activity and Meter producers through OpenTelemetry:
ASP.NET Core requests, outgoing HTTP, runtime metrics and Npgsql database instrumentation.
Retain ordinary console logging, exclude health request traces and export OTLP when its
endpoint is configured. There is no exporter dependency in technical libraries. No global
HTTP retry handler, transaction middleware, background dispatcher or automatic DbContext
configuration is introduced. Native telemetry registration is visible consumer code.

## Proofs and limits

A new active runtime suite uses native Aspire.Hosting.Testing and actual Kestrel/PostgreSQL:

- Fresh graph startup leaves module tables absent and manual setup inactive; liveness is
  healthy and readiness is 503.
- Native start command runs setup to exit 0, after which readiness is healthy and the public
  Alpha/Beta catalog reads return independently expected persisted values.
- Stopping PostgreSQL makes readiness 503 and catalog reads fail while liveness remains
  healthy. The API never reseeds/repairs in response to a failure.
- Tests select `LocalDevelopment:UseDataVolume=false` and randomize ports to isolate their
  disposable graph;
  no personal credentials or already-running application are required.

Focused HTTP/PostgreSQL proofs exercise the actual sample ServiceDefaults with native
in-memory exporters: successful and failed requests retain correlated request/database
Activities and failure logs, and request metrics reach the configured provider. Avoid a
custom protobuf parser or assertions that merely inspect service registration collections.

Also start the graph through `aspire start`, wait via the native CLI, explicitly execute
setup, inspect actual dashboard telemetry through `aspire otel`, and stop it. Report CLI/OTLP
observations separately from automated in-memory exporter and runtime proofs. Neither native
instrumentation alone nor a raw substring in binary OTLP establishes correlation.

Run appropriate active build/style/analyzers/formatter, the runtime and HTTP/PostgreSQL
suites, architecture and independent adoption checks, archive integrity and local links.
Add a separate CI runtime lane; the existing library-only lanes remain independent.
Leave all implementation changes unstaged for owner review.

## Extraction findings

This is sample/template composition. No new reusable library mechanism is assumed. Native
ServiceDefaults wiring is editable source, not a mandatory Foundry runtime package.
Materialized template/CLI output remains E10. The owner narrowed the antiforgery extraction
candidate to an optional human-actor requirement; the current host integration stays local.

E3.7 closes the remaining real OIDC/browser session proof. Membership administration, shared
transactions, deployment, exporter-outage shutdown guarantees and authoritative stock/event
sourcing remain separate capabilities. After bounded E3 completion, E4 returns to durable
event identity and payload codec extraction.

## Native references

The [ServiceDefaults guide](https://aspire.dev/get-started/csharp-service-defaults/) describes
consumer-owned native observability setup. [Explicit resource start](https://aspire.dev/app-host/resource-lifetimes/)
provides manual resource execution. Verify these current pages against pinned package APIs;
newer examples are not automatically available in 13.5.4.
