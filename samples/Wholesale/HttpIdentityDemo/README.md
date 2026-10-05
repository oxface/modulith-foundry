# Wholesale HTTP Access and Organization demonstration

A runnable consumer composing the optional [actor](../../../src/ModulithFoundry.ActorIdentity.AspNetCore/README.md)
and [tenancy](../../../src/ModulithFoundry.Tenancy.AspNetCore/README.md) adapters with native
cookie/OIDC authentication and PostgreSQL-backed Access. Inventory remains a fixture catalog;
messaging and a production development sign-in shortcut are absent.

## Configure and initialize

Use normal .NET configuration, with user secrets or your usual secret provider for credentials.
Environment variables use double underscores instead of colons.

| Key | Meaning |
| --- | --- |
| `ConnectionStrings:Access` | PostgreSQL connection to the consumer's Access database. |
| `Oidc:Authority` | HTTPS OIDC provider authority for HTTP login. |
| `Oidc:ClientId` | Registered authorization-code client. |
| `Oidc:ClientSecret` | Credential when that client registration requires one. |

Set `ConnectionStrings__Access` to a disposable, fresh database and run the finite setup:

```bash
dotnet run --project samples/Wholesale/HttpIdentityDemo/HttpIdentityDemo.csproj -- --initialize-access
```

This mode needs no OIDC configuration. It explicitly migrates schema `access`, uses migration
history `access.__EFMigrationsHistory`, stages demonstration rows, saves within its own native
transaction, commits and exits. Normal HTTP startup neither migrates nor seeds.
Repeated seed reconciliation is unsupported; rerunning setup against populated demo rows
fails instead of rewriting membership. The database is consumer-owned and is not dropped.

The seed contains two global users: `application-alpha` belongs to `wholesale-alpha`
(`north-supply`) and `wholesale-beta` (`south-supply`); `application-beta` belongs only to Beta.
Its external accounts are fictional exact pairs `https://identity.test`/`shared-subject`
and `https://other-identity.test`/`shared-subject`. For actual login, provision your validated
issuer/subject pair against an application user explicitly; neither request mapping nor
setup automatically provisions provider accounts, creates users or links by email.

The [design-time factory](Access/Persistence/AccessDesignTimeFactory.cs) supports native EF tooling:

```bash
dotnet ef database update --project samples/Wholesale/HttpIdentityDemo/HttpIdentityDemo.csproj --context AccessDbContext
```

This applies migrations only. Supply the connection through `ConnectionStrings__Access`.
Native migration scaffolding remains ordinary reviewed consumer C#.

## Run HTTP

Configure OIDC and local HTTPS using native ASP.NET Core certificate setup. Register the
actual `/signin-oidc` callback with your provider, then run:

```bash
dotnet run --project samples/Wholesale/HttpIdentityDemo/HttpIdentityDemo.csproj -- --urls https://localhost:7443
```

Visit `/login` to initiate native OIDC login and return to `/identity`. Without logging in,
`/organizations/north-supply/catalog` and `/organizations/south-supply/catalog` return 42 and 7
available `DEMO-NOTEBOOK` units. These are independently selected fixture quantities, not
persisted Inventory data.

[Program](Program.cs) chooses route selection explicitly through
`AddOrganizationTenancyFromRoute("organization")`. For the tested subdomain alternative,
replace selection registration and endpoint patterns:

```csharp
builder.Services.AddOrganizationTenancyFromSubdomain(baseDomain);
builder.Services.AddHostFiltering(options => options.UseTenantSubdomainHosts(baseDomain));
// After building and calling DemoComposition.ConfigureHttp(app):
DemoComposition.MapOrganizationEndpoints(app, "/catalog", "/tenant-identity");
```

Both presets call the same persisted lookup/admission implementation. Hostname mode reads
`/catalog` and `/tenant-identity` at a selected Organization host such as
`north-supply.wholesale.example.test`. Configure DNS/hosts, certificates and provider
callbacks for your deployment. There is no configuration-driven strategy switch, nullable
domain inference or automatic fallback. Native host filtering includes apex/subdomain
terminal-dot variants; candidate selection rejects nested prefixes. Proxy trust is still
explicit consumer configuration.

## Endpoint and module ownership

| Endpoint | Behavior |
| --- | --- |
| `/health` | Anonymous, tenantless allowed; liveness string, not database readiness. |
| `/identity` | Native authentication required; tenantless allowed; persisted application actor. |
| `/public-identity` | Anonymous, tenantless allowed; mapped authenticated actors remain identified. |
| `/login` | Anonymous, tenantless allowed; native OIDC challenge with a fixed local return path. |
| `/organizations/{organization}/catalog` | Tenant required; explicit public Organization access for anonymous callers and mapped non-members. |
| `/organizations/{organization}/identity` | Native authentication and tenant required; current active membership. |
| `/catalog`, `/tenant-identity` | Subdomain alternatives with the same admission policies. |

[Access](Access/ApplicationAccess.cs) owns global user/external-account lookup, canonical
Organization lookup and membership admission through its [query Contract](Access/Contracts/IApplicationAccess.cs).
Its [DbContext](Access/Persistence/AccessDbContext.cs) queries before tenant establishment,
so it uses explicit keys rather than a current-tenant filter or filter bypass. Member
admission joins the selected Organization and current active membership in one statement.

Issuer/subject pairs compare exactly; email claims grant no identity link. Organization
slugs use lowercase ASCII DNS-compatible labels, accept ASCII case during lookup, and reject
spaces/underscores instead of rewriting them. Human actor keys and tenant keys remain the
same stable application identifiers regardless of external provider or selection strategy.

Every selected Organization requires member admission by default. The catalog explicitly
adds [public-access metadata](Access/PublicOrganizationAccessAttribute.cs) using
`.AllowPublicOrganizationAccess()`; `.AllowAnonymous()` alone does not grant Organization
access. This is consumer policy, separate from native authentication and the library's
tenant requirement. Only human actors enter protected member operations in this sample.

Membership is checked afresh for each protected operation. A committed suspension/removal
blocks the next admission even with the same valid cookie. Already-admitted operations keep
their immutable contexts; this is not serializable authorization through a later business
commit. Membership status and authority are not cached in the cookie or actor/tenant context.

[Inventory](Inventory/FixtureStockCatalog.cs) retains its guarded fixture read and
[Contract](Inventory/Contracts/IStockCatalog.cs). Non-HTTP callers explicitly establish trusted
identity, invoke Access admission when required, and initialize their tenant before business
work. Context establishment itself grants no permission. These folders express ownership
inside one host, not assembly-level module isolation.

[DemoComposition](DemoComposition.cs) keeps routing, native authentication/authorization,
actor completion, tenant admission and endpoint work in the reviewed order. The consumer's
[exception handler](ContextExceptionHandler.cs) returns generic ProblemDetails: actor mapping
failure 403, required selection absent 400, unknown/inaccessible Organization 404. Unrecognized
database faults use native 500 handling. Nothing re-executes identity/tenant establishment.

[NativeAuthentication](NativeAuthentication.cs) retains editable code-flow/PKCE/raw-claim/
secure-cookie configuration. Removing native `iss` deletion preserves the validated issuer
for mapping; token validation remains native.

## Template material, proofs and limits

The executable setup, native registration, Access read Contract, public exception, admission
resolver, schema/history and response policy are editable template recipes. Generated template
files and bootstrap CLI remain E10. Neither technical core nor HTTP adapter depends on Access.

[Composition proofs](../HttpIdentityDemo.Tests/CompositionTests.cs),
[admission proofs](../HttpIdentityDemo.Tests/AdmissionTests.cs) and
[persistence/setup proofs](../HttpIdentityDemo.Tests/PersistenceTests.cs) run on disposable
PostgreSQL 18.6 Testcontainers with the resource reaper enabled. They use native TestServer
and protected cookies without contacting an OIDC provider. They exercise real migrations,
current-membership revocation, public non-member access, route/subdomain parity, constraints,
database faults/cancellation, and an actual finite setup child process.

Run the suite in the active PostgreSQL lane; it is no longer a container-free hook.
Standalone actor/tenancy HTTP suites remain container-free. See
[development commands](../../../docs/development.md) and
[the E3.3 report](../../../docs/reports/e3-3-persisted-access.md).

User creation/linking, profiles, invitations, membership administration, roles/permissions,
session invalidation, remote provider/proxy topology, antiforgery mutations, tenant-aware
native authorization handlers and persisted business ingress remain separate increments.
No mutation endpoint is exposed. E3.4 connects admitted ingress to E2 business persistence
and reviews module project structure.
