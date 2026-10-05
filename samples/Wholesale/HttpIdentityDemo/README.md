# Wholesale HTTP identity and Organization demonstration

A runnable consumer composing the optional [actor](../../../src/ModulithFoundry.ActorIdentity.AspNetCore/README.md)
and [tenancy](../../../src/ModulithFoundry.Tenancy.AspNetCore/README.md) HTTP adapters through
native cookie/OIDC authentication, authorization and Minimal API endpoints. No database,
durable membership, messaging or development header/sign-in shortcut is included.

## Configure and run

Supply keys through normal .NET configuration. Environment variables use double underscores
instead of colons; use user secrets or your usual secret provider for credentials.

| Key | Meaning |
| --- | --- |
| `Oidc:Authority` | HTTPS OIDC provider authority. |
| `Oidc:ClientId` | Registered authorization-code client for this host. |
| `Oidc:ClientSecret` | Credential when required by that registration. |
| `IdentityDirectory:0:Issuer` | Exact validated issuer of a known external account. |
| `IdentityDirectory:0:Subject` | That account's exact subject. |
| `IdentityDirectory:0:UserId` | Stable application-user key. |
| `Organizations:0:Slug` | `north-supply`, selected in a route or hostname. |
| `Organizations:0:Id` | `wholesale-alpha`, its canonical tenant key. |
| `Organizations:1:Slug` | `south-supply`. |
| `Organizations:1:Id` | `wholesale-beta`. |

Additional identity entries are numbered. Duplicate issuer/subject pairs fail configuration;
different pairs can deliberately map to the same user. Email linking, user auto-creation and
membership are not implemented. Organization slugs compare case insensitively in this
consumer directory, while canonical tenant keys retain core exact comparison. Duplicate
slugs fail configuration. Inventory fixtures cover the two canonical keys above.

Configure local HTTPS using normal ASP.NET Core certificate setup. Register the actual
`/signin-oidc` callback address with the provider, then run:

```bash
dotnet run --project samples/Wholesale/HttpIdentityDemo/HttpIdentityDemo.csproj -- --urls https://localhost:7443
```

Visit `/login` to initiate native OIDC login and return to `/identity`. The provider must
already be configured; CI does not need provider credentials. In route mode, anonymous reads
at `/organizations/north-supply/catalog` and `/organizations/south-supply/catalog` return
42 and 7 available `DEMO-NOTEBOOK` units respectively.

Selection is explicit in [Program](Program.cs): `AddOrganizationTenancyFromRoute("organization")`
and route endpoint patterns. The template-local helper keeps Access implementation types
inside their owning sample module and uses the library's `AddRouteTenancy` preset.

For the tested subdomain alternative, replace those startup choices with:

```csharp
// baseDomain can come from deployment configuration; the strategy is selected in code.
builder.Services.AddOrganizationTenancyFromSubdomain(baseDomain);
builder.Services.AddHostFiltering(options => options.UseTenantSubdomainHosts(baseDomain));
// After building the app and calling DemoComposition.ConfigureHttp(app):
DemoComposition.MapOrganizationEndpoints(app, "/catalog", "/tenant-identity");
```

`AddOrganizationTenancyFromSubdomain` uses the library's `AddSubdomainTenancy` preset.
There is no configuration-driven strategy switch or inference from a nullable base domain.
The composition proofs exercise both startup alternatives.

In the subdomain composition, use `/catalog` and `/tenant-identity` with the selected Organization host,
e.g. `north-supply.wholesale.example.test`. Configure DNS/hosts, certificates and provider
callbacks for the addresses you use. Anonymous local HTTP reads can also send an explicit
Host header without DNS provisioning:

```bash
curl -H 'Host: north-supply.wholesale.example.test' http://127.0.0.1:5080/catalog
```

Start the host on that HTTP address for this read; secure cookie login uses configured HTTPS.
The sample configures native host filtering for the base domain and subdomains, including
their terminal-dot forms. Wrong base domains receive native 400 before selection; the helper
rejects nested prefixes. No forwarded-header middleware or proxy trust policy is installed.

## Endpoint and module ownership

| Endpoint | Behavior |
| --- | --- |
| `/health` | Anonymous, tenantless allowed, returns `"healthy"`. |
| `/identity` | Native authentication required; tenantless allowed; application actor kind/key. |
| `/public-identity` | Anonymous, tenantless allowed; authenticated callers retain their actor. |
| `/login` | Anonymous, tenantless allowed; native OIDC challenge with a fixed local return path. |
| `/organizations/{organization}/catalog` | Route mode: anonymous, tenant required, guarded Inventory read. |
| `/organizations/{organization}/identity` | Route mode: native authentication and tenant required. |
| `/catalog`, `/tenant-identity` | Subdomain mode alternatives with the same requirements. |

[Access](Access/OrganizationDirectory.cs) owns canonical Organization lookup and
fixture admission: known Organizations are admitted to demonstration reads. This does not
claim that any application user is a member. The existing external-identity directory is
also consumer fixture policy; durable Access will replace these lookups later.
[Inventory](Inventory/FixtureStockCatalog.cs) owns fresh catalog fixtures and the
[query Contract](Inventory/Contracts/IStockCatalog.cs). The host calls that Contract; the
implementation repeats the core tenant guard for calls outside HTTP. These folders establish
ownership within this small host; they do not prove assembly-level business module isolation.

[Program](Program.cs) selects tenancy registration and endpoint patterns explicitly;
[DemoComposition](DemoComposition.cs) supplies services and composes routing,
native authentication/authorization, actor completion, tenant establishment and endpoints.
Native default/fallback policies require authentication; tenant requirements are independent.
The consumer's [ContextExceptionHandler](ContextExceptionHandler.cs), registered through native
`AddExceptionHandler` alongside `AddProblemDetails`, returns 403 for actor mapping failure,
400 for missing required selection and 404 for failed Organization selection. Native
`UseExceptionHandler()` handles them directly without route re-execution; unsupported
response formats can retain the status without a ProblemDetails body. Unrecognized faults
use the native 500 ProblemDetails fallback. These are
sample decisions, not library response policies. JSON response records are consumer-owned.

[NativeAuthentication](NativeAuthentication.cs) retains editable scheme/code-flow/PKCE/raw-claim/
secure-cookie configuration. It removes the native `iss` deletion action so the validated
issuer survives in the cookie; token validation remains native. Provider callbacks handled
by authentication do not invoke business context establishment.

## Template material, proofs and limits

Copy/edit the exercised registration, pipeline, metadata exceptions, resolver, directory and
host configuration as template material. Route/Subdomain are demonstrated registration
choices; other strategies use a consumer resolver. Generated template files and the bootstrap
CLI remain E10 work. Both libraries remain independently adoptable.

[Composition proofs](../HttpIdentityDemo.Tests/CompositionTests.cs) apply configured native
OIDC claim actions and protect cookie tickets before sending HTTP requests. They prove
issuer/subject mapping without email merging, anonymous/public identity behavior, unknown/
ambiguous identity rejection, the same actor selecting two Organizations, anonymous catalog
data, hostname alternatives, native host filtering and the Inventory capability guard.
Test-only cookie challenge selection never changes the executable's native OIDC setup.

These are fixture reads, not PostgreSQL isolation proofs or durable membership. Native
tenant-aware authorization handlers, real provider/proxy topology, session revocation,
logout, antiforgery-protected mutations and business persistence remain later increments.
No mutation endpoint is exposed. See [E3.1](../../../docs/reports/e3-1-http-actor-identity.md)
and [E3.2](../../../docs/reports/e3-2-http-tenancy.md) for fresh versus historical evidence.
