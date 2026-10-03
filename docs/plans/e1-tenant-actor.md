# E1 tenant and actor context

Status: approved for implementation, 2026-10-03. E1 has now been implemented and verified;
its code awaits owner review. This document preserves the approved scope/interface proposal;
[the implementation report](../reports/e1-tenant-actor.md) records the actual behavior and
new proof results. Read [the design decisions](../design.md) and
[extraction plan](library-extraction.md) alongside it.

## Outcome and scope

Give an ordinary .NET consumer an explicitly established, immutable operation context,
read through an injected accessor. Replace repeated host context-holder logic and context
requirement checks without introducing the sample Access model into technical libraries.

Deliver one package-free runtime library, a finite runnable console sample, library tests
and sample composition tests. Current project locations after the owner-authorized review
rename:

- `src/ModulithFoundry.ExecutionIdentity/`
- `samples/Wholesale/ContextDemo/`
- `tests/ContextTests/`
- `samples/Wholesale/ContextDemo.Tests/`

The sample supplies the exercised template recipe. A separate template implementation is
unnecessary at E1. Add the active root solution with these projects; it excludes the archive.
Project and public type names remain review proposals.

## Agreed behavior

- Tenant, actor and optional initiator stay fixed for an operation. Changing them requires
  a separate operation context/scope.
- A selected tenant and deliberate tenantless execution are distinguishable. Anonymous
  execution is explicit and independent of tenant selection.
- Human and system actors use consumer-supplied identities; actor kind distinguishes equal
  key text. Anonymous actors have no identity key.
- Tenant/actor keys are distinct types with opaque string values. Reject null, empty or
  whitespace-only keys; compare accepted values ordinally and preserve them exactly.
  Domain-ID mapping and canonicalization belong to the consumer.
- The host establishes complete context once per operation scope. Business code reads it
  through an injected read interface. Early reads and repeated establishment are errors.
  There is no reset, replacement or anonymous fallback.
- Establishment precedes parallel business work. Concurrent reads within the same operation
  are supported; separate operations use separate scopes.
- Initiator attribution is optional and explicitly supplied. The library does not infer
  it from the actor or propagate it to another operation.
- Explicit requirement checks supply reusable validation. Consumer policy decides when
  to apply them, admission, membership, permissions and failure presentation.

The sample starts with globally unique application user identities. External identities
resolve to those identities in consumer-owned Access code. E1 does not implement login,
account linking, email-based admission or a production authentication substitute.

## Proposed public interface

Keep one assembly until a real consumer earns another split. It has no package references
or dependency on DI, ASP.NET Core, EF, transports, Aspire or sample Contracts.

| Type | Proposed interface and purpose |
| --- | --- |
| `TenantId`, `ActorId` | Immutable, validated string identity types with value equality. Use sealed reference types initially so a default struct cannot introduce an invalid key. No implicit conversions or GUID parsing. |
| `ActorKind`, `Actor` | Anonymous/human/system distinction; explicit `Anonymous`, `Human(ActorId)` and `System(ActorId)` construction. Construction prevents an anonymous actor carrying a key or an identified actor lacking one. |
| `OperationContext` | Immutable tenant, actor and optional initiator. Explicit `ForTenant(...)` and `Tenantless(...)` factories make the tenant choice deliberate. Initiator uses the same actor representation, allowing explicit anonymous origin or absent attribution. |
| `IOperationContextAccessor` | Read-only `OperationContext Current`; unestablished context raises an error. |
| `IOperationContextInitializer` | Host-facing `Initialize(OperationContext)`; only one successful initialization per operation scope. |
| `OperationContextAccessor` | Concrete scoped holder implementing both interfaces. Atomic publication and single assignment; no ambient/static context or dependency lookup. Proposed disposal behavior is described below. |
| Requirement helpers | `RequireTenantAndIdentifiedActor()` returns both validated values for the common case; `RequireTenant()` and `RequireIdentifiedActor()` support the independent policies. They validate declared context, not identity trust or permission. |

Expose a small typed requirement failure, proposed as `ContextRequirementException` with
a tenant-required or identified-actor-required discriminator. Consumers can handle those
failures without parsing exception messages. Keep HTTP responses and business-denial policy
outside this type. Invalid construction uses ordinary argument exceptions; lifecycle misuse
uses ordinary invalid-operation/object-disposed exceptions.

Context carries identity only. Request IDs, trace state, roles, permissions, principals,
tokens, cancellation and arbitrary metadata bags are excluded from this initial interface.
Transport envelopes and audit persistence explicitly map values later; no stable wire-format
or database mapping is promised by E1.

## Proposed lifecycle and failures

| Situation | Proposed behavior |
| --- | --- |
| New scoped accessor | Unestablished; reading `Current` fails immediately. |
| First initialization with a valid context | Publish the whole immutable value; no partial population. |
| Second initialization, including the same value | Reject without replacing the established context. |
| Competing initialization attempts | At most one succeeds; reject the other. Host code still establishes context before business execution. |
| Concurrent reads after initialization | Return the same immutable context. |
| Tenantless context passed to `RequireTenant()` | Typed tenant-required failure. |
| Anonymous actor passed to `RequireIdentifiedActor()` | Typed identified-actor-required failure. |
| Operation completes, throws or is cancelled | Consumer disposes the owning scope; no ambient state or context is transferred to another operation. |
| Accessor read/initialization after disposal | Proposed: throw `ObjectDisposedException`. Make disposal idempotent, including when DI aliases dispose the same holder more than once. |

Accessor disposal ends further access through that holder. An immutable value already
returned remains an ordinary value; disposal does not revoke identity, cancel work, commit,
rollback or dispose unrelated dependencies. The host awaits its operation branches before
disposing the scope. No nested enter/restore protocol is introduced; an independent child
operation explicitly establishes its own scope/context.

## Consumer wiring

The sample registers the concrete holder as scoped and aliases both interfaces to that same
instance. Registering separate reader and initializer holders would create two contexts and
must be caught by the composition tests. Keep the few registration calls visible in consumer
code; no library registration facade is needed initially.

Illustrative worker composition, using proposed names:

```csharp
services.AddScoped<OperationContextAccessor>();
services.AddScoped<IOperationContextAccessor>(provider =>
    provider.GetRequiredService<OperationContextAccessor>());
services.AddScoped<IOperationContextInitializer>(provider =>
    provider.GetRequiredService<OperationContextAccessor>());

await using var scope = serviceProvider.CreateAsyncScope();
var initializer = scope.ServiceProvider
    .GetRequiredService<IOperationContextInitializer>();
initializer.Initialize(OperationContext.ForTenant(
    new TenantId(consumerTenantKey),
    Actor.System(new ActorId("sales.order-fulfilment")),
    initiator: knownInitiator));

var operation = scope.ServiceProvider.GetRequiredService<ConsumerOperation>();
await operation.RunAsync(cancellationToken);
```

`ConsumerOperation` receives the read accessor through constructor injection. The consumer
owns `consumerTenantKey` mapping and the trust establishing the system actor; accepting an
untrusted actor label or tenant key does not establish permission. The initializer interface
separates roles for normal consumers, not hostile code in the same process.

For a later HTTP composition, authentication and consumer admission resolve identity/tenant
before initializing the scoped accessor. Anonymous/tenantless routes initialize deliberate
context as well. Native authorization owns caller access without a second HTTP actor policy
or actor-specific endpoint extension; tenancy requirements have separate configurable defaults
and exceptions. OIDC registration begins as editable template/sample code. E1 adds no
middleware or principal-to-context population; this integration is planned in E3.

## Sample and independent adoption

Use a finite console composition with standard Microsoft DI and project references. Reuse
the pinned repository SDK/test conventions and select only packages actually needed by
the sample and tests. Exact package versions are verified during implementation; archived
versions are a comparison baseline rather than a new compatibility claim.

Exercise two small wholesale capability calls, proposed as Inventory availability and Sales
draft preview. Consumer-owned fixtures are keyed by tenant and have independently expected
results. Public availability can allow anonymous execution with a selected tenant; the Sales
call requires a selected tenant and identified actor. Define their limited sample ownership
and terminology when adding them. They demonstrate context consumption, not durable stock
or order behavior, production ingress or physical project-level module isolation.

Demonstrate an identified tenantless maintenance operation and an anonymous tenantless
operation separately. Run human and system contexts, with known and absent initiator
attribution. The console has explicitly supplied demonstration identities and exposes no
business HTTP endpoint using fake authentication.

The sample itself demonstrates adoption without the template module graph, Access.Contracts,
EF, ASP.NET Core, Rebus or Aspire. Library tests instantiate the same public interfaces
directly. The only DI dependency belongs to the consumer sample/composition tests.

## Proof obligations and delivery

1. Key validation, exact comparison and preservation; human/system equality distinction;
   factories preventing structurally invalid actor/context combinations.
2. Unestablished reads, one initialization, rejected reassignment and competing initialization;
   concurrent reads of the complete value through the public interfaces.
3. Explicit tenantless/anonymous combinations, typed requirement failures, optional initiator
   and independently supplied attribution.
4. Actual DI alias wiring and scope isolation for concurrent tenant A/B operations. Expected
   capability results come from independent fixtures, not context echo alone.
5. Proposed disposal behavior, idempotent alias disposal, exception/cancellation scope cleanup
   and a subsequent operation receiving only its newly established context.
6. Package-free library build and exercised independent consumer; no references into the
   archive or unwanted runtime dependencies.
7. Runnable console completion with expected results; active build, formatter, analyzers,
   Fast tests and hook/CI coverage. Keep archived lanes intact and explicitly separate.

Review public types, lifecycle and sample wiring first, then implementation, proofs and
limits. Keep the interface and working consumer in one increment; split only if its review
size demands coherent, runnable sub-increments. Leave all changes unstaged. Commit approval
is separate from agreement with this design.

## Findings and remaining limits

The archive supplies a sample-scoped single-assignment accessor, explicit admission/module
checks and issuer/subject-to-user resolution. It does not prove concurrent accessor reads,
disposal invalidation, parallel scope isolation or the new standalone interface.

Reusable candidates are immutable identity/context values, the scoped holder protocol and
explicit requirement helpers. Identity trust, tenant admission, authorization, provider
linking, telemetry enrichment and business behavior remain consumer policy. The template
contribution is explicit DI establishment/lifetime setup.

This plan itself establishes no reusable proof; see the linked report for implementation
results. EF tenant filtering/write guards,
transactions, messaging propagation, production authentication and durable attribution
remain later proof gates. The bootstrap CLI stays in E10.
