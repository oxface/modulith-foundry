# E3.5 Sales profile mutation through cookie-authenticated HTTP

Status: owner-approved scope and interfaces; implemented for review. See [the report](../reports/e3-5-profile-mutation.md) for fresh evidence.
E3.4 remains a separate staged change set. This document records approved scope and source
findings; the report distinguishes fresh executable evidence. No commit is authorized.

## Outcome and scope

An admitted Organization member reads a customer profile and explicitly updates its display
name and one address using the version they observed. A cookie-authenticated JSON mutation
must pass native antiforgery validation before calling Sales. The Sales operation explicitly
begins its own native transaction, performs the two saves, commits, and returns the updated
profile. Stale requests cannot overwrite a committed profile; failure between saves leaves
both rows unchanged.

The owner selected Sales rather than changing Inventory availability. This keeps the small
catalog read view independent of the future authoritative event-sourced stock model and
builds on [E2.4's profile/transaction proof](../reports/e2-4-versioned-profile-changes.md).
That consumer is historical evidence for the proposed new HTTP path, not proof that this
path already works. Keep the existing finite PersistenceDemo source and tests independent.

This demonstration permits any active human Organization member to edit the selected
Organization's customer profile. Membership is admission, not a general role/permission
system. Preserve E3.3's admission-time revocation semantics: committed revocation blocks
the next admission; already-admitted work can finish. There is no membership lock spanning
the business commit. Review stronger authority requirements as a separate capability.

No actor/tenancy/persistence library change is assumed. Native HTTP protection and Sales
business behavior remain consumer-owned code; template materialization stays E10.

## Sales projects and Contract

Add populated `samples/Wholesale/modules/Sales/Sales` and `Sales.Contracts` projects, following
the reviewed Access/Inventory composition pattern. Sales owns tenant-isolated customer and
address rows, versioning and profile changes. It references its own Contracts, Tenancy, the
existing EF ownership utility and the selected native EF/Npgsql dependencies. It references
neither peer implementations nor the HTTP host. Contracts contain no EF/HTTP/context types.

The approved consumer interface and immutable data:

```csharp
public interface ICustomerProfiles
{
    Task<CustomerProfile?> ReadAsync(Guid customerId, CancellationToken cancellationToken);
    Task<ProfileChangeResult> ChangeAsync(
        CustomerProfileChange change, CancellationToken cancellationToken);
}

public sealed record CustomerProfile(
    Guid CustomerId, Guid AddressId, string Code,
    string DisplayName, string AddressLine, long Version);

public sealed record CustomerProfileChange(
    Guid CustomerId, Guid AddressId, long ExpectedVersion,
    string DisplayName, string AddressLine);

public abstract record ProfileChangeResult
{
    public sealed record Updated(CustomerProfile Profile) : ProfileChangeResult;
    public sealed record NotFound : ProfileChangeResult;
    public sealed record Conflict : ProfileChangeResult;
}
```

The result cases are specific to this consumer capability, not a reusable result framework.
Validate nonempty IDs, nonblank display/address text and an expected version in
`1..long.MaxValue-1`; do not silently trim accepted text. Host validation presents invalid
input as 400; direct Contract misuse receives ordinary argument exceptions. Keep validation
explicit without adding a validation framework or generator. Review database length limits
and matching consumer validation together when implementing the model.

The Contract accepts no tenant or actor supplied by a request body. Sales obtains the
established tenant through its accessor. HTTP admission and antiforgery are host obligations;
trusted non-HTTP callers explicitly authorize/admit and establish their operation context.
Context establishment alone grants no permission. A successful change means this specific
Sales transaction committed; it does not merely stage work into a caller's ambient transaction.
Shared transactions and cross-module workflows remain separate capabilities.

## HTTP and native antiforgery

| Endpoint | Native authentication | Tenant/admission | Behavior |
| --- | --- | --- | --- |
| `GET /antiforgery` | Required | Tenantless allowed | Issue native request token/cookie after application actor establishment; response is not cacheable. |
| `GET /organizations/{organization}/customers/{customerId:guid}/profile` | Required | Required; active membership | Read the tenant-owned profile and current version. |
| `PUT /organizations/{organization}/customers/{customerId:guid}/profile` | Required | Required; active membership | Validate native antiforgery, then apply the explicit versioned profile change. |

The PUT body supplies AddressId, ExpectedVersion, DisplayName and AddressLine; CustomerId
comes from the route. Return 200 with the committed profile, 404 for an absent customer or
an address not belonging to that selected customer/tenant, 409 for an expected-version
conflict, and 400 for invalid input or antiforgery failure. Preserve native 401 and existing
actor/admission failure responses. Unexpected database errors use the native 500 handler.
Responses do not reveal foreign-tenant existence.

Register native `AddAntiforgery` in editable host configuration. Set an explicit header name
such as `X-CSRF-TOKEN` and a host-only HttpOnly cookie with SecurePolicy.Always, SameSite.Strict
and path `/`. Native `GetAndStoreTokens` issues the cookie/request-token pair; clients send
the request token in the configured header with their existing authentication cookie.
Token issuance requires no selected Organization and saves no business data. Acquire a fresh token
after changing login identity. Do not expose tokens through URL parameters or request logs.

For this JSON endpoint, explicitly attach a small host-owned native endpoint filter that
calls `IAntiforgery.ValidateRequestAsync` and returns generic 400 on its validation exception
before invoking the handler. Authentication, actor completion and tenancy admission run
first. Invalid antiforgery must invoke no Sales Contract or transaction. Do not assume
adding antiforgery middleware alone enforces JSON handlers: the native middleware records
a verdict rather than short-circuiting endpoint execution, as shown by the
[pinned middleware implementation](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Antiforgery/src/AntiforgeryMiddleware.cs).
Native APIs are documented in
[Microsoft's antiforgery guide](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).
No transaction, retry or save is performed by this filter. Keep its attachment explicit.

### Binding to the application actor

Native antiforgery also derives identity from the principal's subject claim and its
`Claim.Issuer`, rather than interpreting the application's separate `iss` claim or Access
lookup. The [pinned 10.0.12 implementation](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Antiforgery/src/Internal/DefaultClaimUidExtractor.cs)
is the source finding; do not infer application-user binding from claim binding alone.

Use a host-owned `IAntiforgeryAdditionalDataProvider` which embeds the established human
ActorId and compares it exactly during validation. Retain native token protection and
principal/cookie checks. This is explicit binding to the current application identity,
not an alternative token format or a membership cache. Tokens are not bound to one
Organization: the same actor can select multiple Organizations, each independently admitted.

The [native additional-data interface](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Antiforgery/src/IAntiforgeryAdditionalDataProvider.cs)
is intended for supplemental token data. Native antiforgery services are
[singleton registrations](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Antiforgery/src/AntiforgeryServiceCollectionExtensions.cs):
the provider must not capture a scoped actor accessor in its constructor. Obtain the
request-scoped accessor through the supplied HttpContext.RequestServices while generating
or validating data. Actor establishment must already be complete; missing actor fails
instead of supplying an empty binding. This adapter is a potential later extraction
candidate if actual reuse removes repeated integration complexity; it starts in the host.

## Persistence, versioning and transaction ownership

Sales uses schema `sales` and history `sales.__EFMigrationsHistory` in the host's shared
database. Use an initial native migration for this new consumer model; do not import or
rewrite the finite E2 proof's migrations. This does not promise migration compatibility
with that separate demo database. Preserve existing Access/Inventory rows and histories
when Sales migration is applied to their database.

Register E2 tenant ownership explicitly on both rows. A tenant-bearing address/customer
foreign key enforces that an address's Organization matches its parent; queries additionally
select the requested customer/address pair within the current tenant. Code uniqueness is
per Organization. Version starts at 1, is a separate native concurrency token and is advanced
explicitly by the profile operation, without interceptors or database-generated increments.

The internal Sales operation owns native begin/save/commit/rollback calls in ordinary C#.
It requires established tenancy and validated input, loads the scoped rows, and uses the
caller's ExpectedVersion as the original concurrency value even when the server reloads
a newer row. Explicitly advance the customer version, save its display name/version, then
save the address, and commit only when both succeed. Native affected-row concurrency checks
handle a race after loading; an initial comparison alone is insufficient.

Convert the native concurrency exception into the consumer Conflict case after rollback.
Missing rows return NotFound without committing changes. Other failures/cancellation roll
back and propagate, with cleanup not using an already-cancelled request token. Discard the
failed context; a later operation creates a fresh scope. Do not retry/rebase automatically.
Pass request cancellation to native reads, saves and commit. Cancellation after an HTTP
response is lost does not prove that a transaction never committed; this slice supplies no
durable operation receipt or exactly-once/idempotent HTTP contract.

The host explicitly registers Sales DbContext options and its internal Contract binding.
Extend full-demo setup with native Sales migration and tenant-scoped staging/save/commit;
keep Access-only mode and ordinary startup behavior. Full setup remains a fresh disposable
demonstration workflow, without atomic cross-module setup or automatic runtime migration.
No customer create/delete, general address management, audit staging or event publication
is needed for this profile edit.

## Executable proofs and value

Extend the same PostgreSQL 18.6/native TestServer consumer. Use protected native cookies and
real antiforgery tokens obtained over HTTP; no fake validator or production timing hook.
TestServer HTTPS request semantics exercise Secure cookies but do not prove a TLS handshake
or browser/subdomain/OIDC session topology. Add focused cases for these consumer obligations:

| Proof | Independent evidence |
| --- | --- |
| Successful edit | Read Alpha version 1, obtain token/cookies, PUT name/address, observe version 2 and both new values through a fresh read; Beta's profile is unchanged. |
| Tenant/pair isolation | Foreign customer IDs or an address from another customer/tenant yield 404 and change neither profile; ordinary Contract calls without tenancy fail. |
| Admission before writes | Anonymous/unmapped/non-member/revoked callers receive native/mapping/admission failures without any Sales effect, including when Sales is unavailable. |
| Cookie mutation protection | Missing or invalid request token/cookie yields generic 400 before any business change. Valid native protection permits the real write. Existing GET catalog/health behavior remains usable. |
| Application identity binding | A token issued for Alpha cannot authorize a mutation by Beta. Also explicitly reassign a provider link in trusted test setup while keeping the exact principal/authentication cookie unchanged: the previously issued token fails for the newly mapped application actor, isolating the additional binding from native claim checks. |
| Organization selection | A token for the same actor remains usable for an independently admitted second Organization; public catalog access alone grants no profile authority. |
| Stale and competing writes | Two clients observe version 1. One succeeds; the stale edit returns 409 and leaves winner values intact even after server reload. Concurrent edits yield one commit and one conflict. |
| Second-save failure | A test-only PostgreSQL constraint/trigger rejects the address update after the customer save. Observe 500 and unchanged name/address/version in a fresh scope; repair permits a new request. |
| Cancellation before commit | A native address-table lock permits reads but blocks its UPDATE after the first save. Wait for that statement via an autocommit observer, cancel, then verify both rows/version rolled back and a fresh operation succeeds. |
| Module ownership/setup | Apply Sales migration alongside existing Access/Inventory data and separate histories; full setup supplies actual HTTP profile witnesses, ordinary startup creates no schemas. Extend real-assembly architecture checks to Sales and its Contracts. |

These are integration/policy proofs, not tests of native cryptography, token serialization,
the framework's entire antiforgery matrix or another generic architecture checker. Use
independently expected values and actual SQL failure points. Keep archive sources/fixtures
unchanged. Existing E2 results are historical until rerun; native source findings are not
test results for the proposed host adapter.

Run the new HTTP/PostgreSQL suite, relevant E2 PostgreSQL suites, independent library/adapter
consumers, architecture checks, active build/style/analyzers, formatting and archive/link
verification. Document fresh counts, actual commands and rejected assumptions. Leave all
new implementation changes unstaged for owner review.

## Findings to report and remaining scope

**Library:** assess whether existing ownership/context seams suffice for mutation. No new
reusable mechanism is currently proven. Actor-aware native antiforgery binding is a concrete
candidate to reassess after executable evidence and another useful adoption scenario.

**Template:** exercise native cookie JSON protection, token issuance, application-actor
binding, typed consumer outcomes and visible native transaction handling in populated
module/Contracts projects. Security configuration and admission remain editable policy.

**Sample:** Sales owns profile changes, Access owns fresh admission, Inventory retains its
read capability. Membership administration, invitations, account linking and permissions
remain future Access capabilities with a deliberate reuse review before promotion.

E3 remains open for actual OIDC/session/proxy topology and Aspire ServiceDefaults. Shared
module transactions, durable HTTP receipts, audit and authoritative stock behavior require
their own later proofs. Owner review of the implementation remains required; approval of implementation does not authorize a commit.
