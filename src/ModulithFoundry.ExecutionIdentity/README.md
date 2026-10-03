# Execution identity

`ModulithFoundry.ExecutionIdentity` is a package-free .NET 10 library. It supplies immutable tenant,
actor and optional initiator values; a scope-owned accessor; and explicitly called
requirement checks. It has no DI, web, database, transport, Aspire or Access dependency.
Its scope is operation identity and tenant selection; other execution concerns belong to
their own capabilities.

## Identity and context

Create `TenantId` and `ActorId` from consumer-owned canonical string keys. Values compare
ordinally and retain their exact text. Null, empty and whitespace-only keys are rejected.
Human and system actors with the same key are different identities. `Actor.Anonymous`
has no key.

Use `OperationContext.ForTenant(tenant, actor, initiator)` or
`OperationContext.Tenantless(actor, initiator)` deliberately. A null tenant on a returned
context means tenantless execution; an uninitialized accessor is a separate error.
Anonymous execution and tenant selection are independent. Initiator is optional and is
never inferred, propagated or used as authority.

Authentication establishes trust. The consumer maps external identity to its application
actor key, decides tenant admission, and checks permission. Context expresses the identities
the consumer supplied; it does not perform those checks.

## Explicit scope wiring

Register `OperationContextAccessor` with the consumer's operation scope. Alias
`IOperationContextAccessor` and `IOperationContextInitializer` to that same holder. See
[the runnable sample composition](../../samples/Wholesale/ContextDemo/DemoComposition.cs)
and [scope establishment](../../samples/Wholesale/ContextDemo/Program.cs).

The host initializes complete context once, before business execution. Business code receives
the read accessor through constructor injection and calls `Current`. Parallel branches of
one operation can read it concurrently. Separate operations establish separate scopes;
creating a child DI scope does not inherit context.

The consumer awaits all operation branches, then disposes its scope on success, exception
or cancellation. The holder owns no transaction or external resource. Disposing it prevents
future reads/initialization; already returned immutable values remain usable. Cancellation
does not dispose a still-running operation automatically.

## Requirements and failures

For operations requiring both, `context.RequireTenantAndIdentifiedActor()` returns the
selected tenant and human/system actor together. It checks tenant first and preserves the
typed failure reason for whichever requirement fails. `RequireTenant()` and
`RequireIdentifiedActor()` cover deliberately different capability policies. These checks
are explicitly called by the consumer; an identified actor can still lack business permission.

| Situation | Failure |
| --- | --- |
| Null construction input | `ArgumentNullException` |
| Empty or whitespace-only identity key | `ArgumentException` |
| Read before initialization; any repeated initialization | `InvalidOperationException` |
| Read/initialize after disposal | `ObjectDisposedException` |
| `RequireTenant()` on tenantless context | `ContextRequirementException` with `TenantRequired` |
| `RequireIdentifiedActor()` on anonymous context | `ContextRequirementException` with `IdentifiedActorRequired` |

Enum members have explicit nonzero values; 0 is invalid. Context factories do not accept
raw actor kinds. External/persistence mappings must validate raw enum values separately.

Initialization/disposal are serialized with reads. Competing initializers have one winner;
the established value is never replaced. Disposal is idempotent for consumers that alias
the same holder under multiple DI registrations.

The context does not store principals, roles, tokens, trace state or request metadata.
There is no ambient context, middleware, automatic registration or persistence behavior.
Transport/audit mappings and their schema compatibility are separate consumer decisions.

## Planned HTTP integration

E3 will exercise a separately adopted ASP.NET Core adapter. Native `[Authorize]`,
`.RequireAuthorization()` and `AllowAnonymous` will control HTTP caller access without a
parallel actor policy or an actor-specific endpoint extension. Consumer-provided resolution
will map the effective authenticated principal to the application actor and resolve tenant
identity/admission before initializing complete context. Tenant requirements will have
consumer-selected defaults and explicit tenantless exceptions, independently of native
authorization. Authenticated-principal mapping failures must not become anonymous execution.

OIDC/cookie registration will start as editable sample/template code. None of these HTTP
behaviors is implemented or proven by E1; see [the integration direction](../../docs/design.md#scope-and-http-integration-review)
and [E3 plan](../../docs/plans/library-extraction.md#e3-trusted-ingress-and-state-stored-sample-path).
