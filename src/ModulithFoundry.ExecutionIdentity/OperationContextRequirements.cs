namespace ModulithFoundry.ExecutionIdentity;

public static class OperationContextRequirements
{
    /// <summary>Requires both a selected tenant and an identified actor, checking tenant first.</summary>
    public static (TenantId Tenant, Actor Actor) RequireTenantAndIdentifiedActor(
        this OperationContext context
    )
    {
        TenantId tenant = context.RequireTenant();
        Actor actor = context.RequireIdentifiedActor();
        return (tenant, actor);
    }

    public static TenantId RequireTenant(this OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Tenant
            ?? throw new ContextRequirementException(ContextRequirement.TenantRequired);
    }

    /// <summary>Checks identity presence only; the consumer must establish trust and permission.</summary>
    public static Actor RequireIdentifiedActor(this OperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Actor.Kind == ActorKind.Anonymous)
        {
            throw new ContextRequirementException(ContextRequirement.IdentifiedActorRequired);
        }

        return context.Actor;
    }
}
