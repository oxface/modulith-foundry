using ModulithFoundry.ExecutionIdentity;

namespace ModulithFoundry.ContextTests;

public sealed class IdentityAndContextTests
{
    [Fact]
    public void NullKeysAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new TenantId(null!));
        Assert.Throws<ArgumentNullException>(() => new ActorId(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    [InlineData("\u00a0")]
    public void BlankKeysAreRejected(string key)
    {
        Assert.Throws<ArgumentException>(() => new TenantId(key));
        Assert.Throws<ArgumentException>(() => new ActorId(key));
    }

    [Theory]
    [InlineData("ACME", "acme")]
    [InlineData("é", "e\u0301")]
    [InlineData(" 42", "42")]
    [InlineData("42 ", "42")]
    [InlineData("00042", "42")]
    public void KeysPreserveExactValuesAndCompareOrdinally(string first, string second)
    {
        var tenant = new TenantId(first);
        var actor = new ActorId(first);

        Assert.Equal(first, tenant.Value);
        Assert.Equal(first, actor.Value);
        Assert.Equal(tenant, new TenantId(first));
        Assert.Equal(actor, new ActorId(first));
        Assert.Equal(tenant.GetHashCode(), new TenantId(first).GetHashCode());
        Assert.Equal(actor.GetHashCode(), new ActorId(first).GetHashCode());
        Assert.NotEqual(tenant, new TenantId(second));
        Assert.NotEqual(actor, new ActorId(second));
        Assert.False(tenant.Equals((object)actor));
    }

    [Fact]
    public void IdentifiedActorsRequireKeysAndActorKindParticipatesInEquality()
    {
        Assert.Throws<ArgumentNullException>(() => Actor.Human(null!));
        Assert.Throws<ArgumentNullException>(() => Actor.System(null!));

        Actor human = Actor.Human(new ActorId("42"));
        Actor system = Actor.System(new ActorId("42"));
        Assert.Equal(human, Actor.Human(new ActorId("42")));
        Assert.Equal(system, Actor.System(new ActorId("42")));
        Assert.NotEqual(human, system);
        Assert.NotEqual(human, Actor.Anonymous);
        Assert.Equal(ActorKind.Anonymous, Actor.Anonymous.Kind);
        Assert.Null(Actor.Anonymous.Id);
    }

    [Fact]
    public void ContextFactoriesRejectMissingRequiredConstructionValues()
    {
        Assert.Throws<ArgumentNullException>(() =>
            OperationContext.ForTenant(null!, Actor.Anonymous)
        );
        Assert.Throws<ArgumentNullException>(() =>
            OperationContext.ForTenant(new TenantId("alpha"), null!)
        );
        Assert.Throws<ArgumentNullException>(() => OperationContext.Tenantless(null!));
    }

    [Theory]
    [InlineData(false, ActorKind.Anonymous)]
    [InlineData(false, ActorKind.Human)]
    [InlineData(false, ActorKind.System)]
    [InlineData(true, ActorKind.Anonymous)]
    [InlineData(true, ActorKind.Human)]
    [InlineData(true, ActorKind.System)]
    public void TenantSelectionAndActorKindAreIndependent(bool selectTenant, ActorKind kind)
    {
        var tenant = new TenantId("alpha");
        Actor actor = kind switch
        {
            ActorKind.Anonymous => Actor.Anonymous,
            ActorKind.Human => Actor.Human(new ActorId("user-42")),
            ActorKind.System => Actor.System(new ActorId("workflow")),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        OperationContext context = selectTenant
            ? OperationContext.ForTenant(tenant, actor)
            : OperationContext.Tenantless(actor);

        Assert.Same(actor, context.Actor);
        Assert.Null(context.Initiator);
        if (selectTenant)
        {
            Assert.Same(tenant, context.RequireTenant());
        }
        else
        {
            var failure = Assert.Throws<ContextRequirementException>(() => context.RequireTenant());
            Assert.Equal(ContextRequirement.TenantRequired, failure.Requirement);
        }

        if (kind == ActorKind.Anonymous)
        {
            var failure = Assert.Throws<ContextRequirementException>(() =>
                context.RequireIdentifiedActor()
            );
            Assert.Equal(ContextRequirement.IdentifiedActorRequired, failure.Requirement);
        }
        else
        {
            Assert.Same(actor, context.RequireIdentifiedActor());
        }

        if (!selectTenant || kind == ActorKind.Anonymous)
        {
            var failure = Assert.Throws<ContextRequirementException>(() =>
                context.RequireTenantAndIdentifiedActor()
            );
            Assert.Equal(
                selectTenant
                    ? ContextRequirement.IdentifiedActorRequired
                    : ContextRequirement.TenantRequired,
                failure.Requirement
            );
        }
        else
        {
            var required = context.RequireTenantAndIdentifiedActor();
            Assert.Same(tenant, required.Tenant);
            Assert.Same(actor, required.Actor);
        }
    }

    [Fact]
    public void InitiatorIsExplicitOptionalAttributionAndDoesNotIdentifyAnonymousActor()
    {
        Actor human = Actor.Human(new ActorId("user-42"));
        Actor workflow = Actor.System(new ActorId("workflow"));
        OperationContext deferred = OperationContext.Tenantless(workflow, human);
        OperationContext fresh = OperationContext.Tenantless(workflow);
        OperationContext anonymous = OperationContext.Tenantless(Actor.Anonymous, human);
        OperationContext anonymousOrigin = OperationContext.Tenantless(workflow, Actor.Anonymous);

        Assert.Same(workflow, deferred.RequireIdentifiedActor());
        Assert.Same(human, deferred.Initiator);
        Assert.Null(fresh.Initiator);
        Assert.NotEqual(deferred, fresh);
        Assert.Same(Actor.Anonymous, anonymousOrigin.Initiator);
        Assert.Throws<ContextRequirementException>(() => anonymous.RequireIdentifiedActor());
    }

    [Fact]
    public void RequirementChecksRejectMissingContext()
    {
        OperationContext absent = null!;
        Assert.Throws<ArgumentNullException>(() => absent.RequireTenant());
        Assert.Throws<ArgumentNullException>(() => absent.RequireIdentifiedActor());
        Assert.Throws<ArgumentNullException>(() => absent.RequireTenantAndIdentifiedActor());
    }
}
