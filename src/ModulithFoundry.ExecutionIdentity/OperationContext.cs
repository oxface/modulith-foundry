namespace ModulithFoundry.ExecutionIdentity;

/// <summary>An immutable tenant choice, executing actor and optional origin attribution.</summary>
public sealed record OperationContext
{
    private OperationContext(TenantId? tenant, Actor actor, Actor? initiator)
    {
        ArgumentNullException.ThrowIfNull(actor);
        Tenant = tenant;
        Actor = actor;
        Initiator = initiator;
    }

    /// <summary>Null means deliberate tenantless execution in an established context.</summary>
    public TenantId? Tenant { get; }

    public Actor Actor { get; }

    /// <summary>Optional attribution supplied by the consumer; it grants no authority.</summary>
    public Actor? Initiator { get; }

    public static OperationContext ForTenant(TenantId tenant, Actor actor, Actor? initiator = null)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        return new OperationContext(tenant, actor, initiator);
    }

    public static OperationContext Tenantless(Actor actor, Actor? initiator = null) =>
        new(null, actor, initiator);
}
