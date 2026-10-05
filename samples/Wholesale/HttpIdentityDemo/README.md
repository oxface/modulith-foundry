# Wholesale HTTP identity demonstration

A runnable actor-only consumer of [the ASP.NET Core adapter](../../../src/ModulithFoundry.ActorIdentity.AspNetCore/README.md).
It uses native cookie/OIDC authentication, authorization and Minimal API endpoints. There
is no tenancy, EF, durable Access or messaging dependency and no development header/sign-in
shortcut in the executable.

## Configure and run

Supply these keys through normal .NET configuration. Environment variables use double
underscores in place of colons; use user secrets or your usual secret provider for credentials.

| Key | Meaning |
| --- | --- |
| `Oidc:Authority` | Your HTTPS OIDC provider authority. |
| `Oidc:ClientId` | Registered authorization-code client for this host. |
| `Oidc:ClientSecret` | Client credential when required by the provider's client registration. |
| `IdentityDirectory:0:Issuer` | Exact validated issuer identifying a known external account. |
| `IdentityDirectory:0:Subject` | That account's exact subject. |
| `IdentityDirectory:0:UserId` | Stable consumer application-user key, distinct from provider identity. |

Add further numbered directory entries for additional accounts. Duplicate issuer/subject
pairs fail configuration. Different pairs can explicitly point to the same application user;
there is no email linking, user auto-creation or membership policy. The dictionary is an
immutable fixture; durable Access will own this behavior in a later slice.

Configure local HTTPS using your normal ASP.NET Core certificate setup, register
`https://localhost:7443/signin-oidc` as the provider callback for that address, then run:

```bash
dotnet run --project samples/Wholesale/HttpIdentityDemo/HttpIdentityDemo.csproj -- --urls https://localhost:7443
```

Visit `/login` to initiate the native OIDC challenge and return to `/identity`. The provider
must already be available and configured; this slice does not provision it or need personal
provider credentials for its CI proofs.

## Endpoints and consumer policy

| Endpoint | Behavior |
| --- | --- |
| `/health` | Native anonymous endpoint returning `"healthy"`. |
| `/identity` | Requires native authentication; returns application actor kind/key. |
| `/public-identity` | Allows anonymous access; an authenticated caller keeps their resolved actor. |
| `/login` | Native OIDC challenge with a fixed local return path. |

`IdentityResponse` is consumer-owned JSON, not a stable library wire contract. Kind values
follow the core's explicit enum numbers (Anonymous 1, Human 2, System 3). This resolver
produces Human for a known pair and permits exactly one authenticated identity with exactly
one issuer and subject claim. Ambiguous or unknown authenticated identities receive the
sample's generic 403 problem response, even on a public endpoint. The adapter selects no
response status itself.

[NativeAuthentication](NativeAuthentication.cs) visibly selects cookie/OIDC schemes, code
flow/PKCE, raw claim names and secure cookies. It removes the native `iss` deletion action
so the validated issuer survives into the cookie used by the resolver. Retaining that claim
does not bypass token validation. Callback processing remains in native authentication;
requests handled there do not invoke business actor establishment.

[DemoComposition](DemoComposition.cs) opts into the library's
`AddHttpActorContext<DirectoryActorResolver>()` and `UseHttpActorContext()` helpers.
These group holder/alias/resolver/evaluator registration and completion middleware;
native authentication/authorization setup and pipeline order remain visible in the sample.
Native default and fallback policies require authentication. The outer mapping-failure
handler writes its response without re-executing authentication. Copy/edit these exercised
files as template material. Directory and failure behavior remain consumer-owned.

## Proofs and remaining scope

[Composition proofs](../HttpIdentityDemo.Tests/CompositionTests.cs) apply the actual configured
native OIDC claim actions to fixture identities, protect native cookie tickets, and send HTTP
requests through this composition. They prove issuer/subject mapping without email merging,
anonymous/public identity behavior and the consumer's unknown/ambiguous identity response.
Cookie-only challenge selection is explicit test setup; the production sample selects OIDC.

This proves the claim-action and cookie/request composition, not provider token validation,
remote login/callback traffic or complete BFF/session security. Live provider topology,
logout/session revocation, antiforgery-protected mutations, Access persistence and tenant
admission remain separate E3 increments. No business mutation endpoint is exposed here.
See [the slice report](../../../docs/reports/e3-1-http-actor-identity.md).
