using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.ActorIdentity;
using ModulithFoundry.Samples.Wholesale.ContextDemo.Inventory.Contracts;
using ModulithFoundry.Samples.Wholesale.ContextDemo.Sales.Contracts;
using ModulithFoundry.Tenancy;

namespace ModulithFoundry.Samples.Wholesale.ContextDemo.Tests;

public sealed class CompositionTests
{
    [Fact]
    public async Task ActualRegistrationSharesEachHolderAndDisposesAllAliasesSafely()
    {
        await using ServiceProvider provider = CreateProvider();
        IActorContextAccessor actors;
        IActorContextInitializer actorInitializer;
        ITenantContextAccessor tenants;
        ITenantContextInitializer tenantInitializer;
        var actorContext = new ActorContext(Actor.Anonymous);
        TenantContext tenantContext = TenantContext.ForTenant(new TenantId("wholesale-alpha"));
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            actors = scope.ServiceProvider.GetRequiredService<IActorContextAccessor>();
            actorInitializer = scope.ServiceProvider.GetRequiredService<IActorContextInitializer>();
            tenants = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            tenantInitializer =
                scope.ServiceProvider.GetRequiredService<ITenantContextInitializer>();
            Assert.Same(actors, actorInitializer);
            Assert.Same(actors, scope.ServiceProvider.GetRequiredService<ActorContextAccessor>());
            Assert.Same(tenants, tenantInitializer);
            Assert.Same(tenants, scope.ServiceProvider.GetRequiredService<TenantContextAccessor>());
            Assert.NotSame(actors, tenants);
            actorInitializer.Initialize(actorContext);
            tenantInitializer.Initialize(tenantContext);
            Assert.Same(actorContext, actors.Current);
            Assert.Same(tenantContext, tenants.Current);
            Assert.Equal(
                42,
                scope
                    .ServiceProvider.GetRequiredService<IStockAvailability>()
                    .GetAvailableQuantity("WIDGET")
            );
        }

        Assert.Throws<ObjectDisposedException>(() => actors.Current);
        Assert.Throws<ObjectDisposedException>(() => actorInitializer.Initialize(actorContext));
        Assert.Throws<ObjectDisposedException>(() => tenants.Current);
        Assert.Throws<ObjectDisposedException>(() => tenantInitializer.Initialize(tenantContext));
        Assert.Equal("wholesale-alpha", tenantContext.RequireTenant().Value);
        Assert.Same(Actor.Anonymous, actorContext.Actor);
    }

    [Fact]
    public async Task ActorOnlyCompositionNeedsNoTenancyRegistration()
    {
        await using ServiceProvider provider = Build(DemoComposition.CreateActorServices());
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        Assert.Null(scope.ServiceProvider.GetService<ITenantContextAccessor>());
        Assert.Null(scope.ServiceProvider.GetService<ITenantContextInitializer>());
        var human = Actor.Human(new ActorId("global-user-42"));
        scope
            .ServiceProvider.GetRequiredService<IActorContextInitializer>()
            .Initialize(new ActorContext(Actor.System(new ActorId("maintenance")), human));
        ActorContext current = scope
            .ServiceProvider.GetRequiredService<IActorContextAccessor>()
            .Current;
        Assert.Equal("maintenance", current.RequireIdentifiedActor().Id!.Value);
        Assert.Same(human, current.Initiator);
    }

    [Theory]
    [InlineData("wholesale-alpha", 42)]
    [InlineData("wholesale-beta", 7)]
    [InlineData("unknown-tenant", 0)]
    [InlineData("WHOLESALE-ALPHA", 0)]
    public async Task InventoryNeedsOnlyTenancyRegistration(string tenant, int expected)
    {
        await using ServiceProvider provider = Build(DemoComposition.CreateTenantServices());
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        Assert.Null(scope.ServiceProvider.GetService<IActorContextAccessor>());
        Assert.Null(scope.ServiceProvider.GetService<IActorContextInitializer>());
        scope
            .ServiceProvider.GetRequiredService<ITenantContextInitializer>()
            .Initialize(TenantContext.ForTenant(new TenantId(tenant)));
        Assert.Equal(
            expected,
            scope
                .ServiceProvider.GetRequiredService<IStockAvailability>()
                .GetAvailableQuantity("WIDGET")
        );
    }

    [Theory]
    [InlineData("wholesale-alpha", 42, true)]
    [InlineData("wholesale-beta", 7, false)]
    public async Task SameActorCanUseDifferentTenantsThroughSalesAndInventory(
        string tenant,
        int expected,
        bool canFulfil
    )
    {
        await using ServiceProvider provider = CreateProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        Actor human = Actor.Human(new ActorId("global-user-42"));
        Establish(scope, TenantContext.ForTenant(new TenantId(tenant)), human);
        DraftPreview preview = scope
            .ServiceProvider.GetRequiredService<IDraftOrderPreview>()
            .Preview("WIDGET", 10);
        Assert.Equal("WIDGET", preview.Sku);
        Assert.Equal(10, preview.RequestedQuantity);
        Assert.Equal(expected, preview.AvailableQuantity);
        Assert.Equal(canFulfil, preview.CanFulfil);
        Assert.Same(human, preview.RequestedBy);
        Assert.Null(preview.Initiator);
    }

    [Fact]
    public async Task WorkflowAttributionKeepsExecutingSystemAndOriginalHumanDistinct()
    {
        await using ServiceProvider provider = CreateProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        Actor workflow = Actor.System(new ActorId("sales.order-fulfilment"));
        Actor initiator = Actor.Human(new ActorId("global-user-42"));
        Establish(
            scope,
            TenantContext.ForTenant(new TenantId("wholesale-beta")),
            workflow,
            initiator
        );
        DraftPreview preview = scope
            .ServiceProvider.GetRequiredService<IDraftOrderPreview>()
            .Preview("WIDGET", 10);
        Assert.Equal(7, preview.AvailableQuantity);
        Assert.False(preview.CanFulfil);
        Assert.Same(workflow, preview.RequestedBy);
        Assert.Same(initiator, preview.Initiator);
    }

    [Fact]
    public async Task UnestablishedAndTenantlessCallsFailRatherThanReturningOtherTenantData()
    {
        await using ServiceProvider provider = CreateProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IStockAvailability stock = scope.ServiceProvider.GetRequiredService<IStockAvailability>();
        Assert.Throws<InvalidOperationException>(() => stock.GetAvailableQuantity("WIDGET"));
        scope
            .ServiceProvider.GetRequiredService<ITenantContextInitializer>()
            .Initialize(TenantContext.Tenantless());
        Assert.Throws<TenantRequiredException>(() => stock.GetAvailableQuantity("WIDGET"));
    }

    [Fact]
    public async Task HumanInitiatorCannotMakeAnAnonymousActorEligibleForSales()
    {
        await using ServiceProvider provider = CreateProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        Establish(
            scope,
            TenantContext.ForTenant(new TenantId("wholesale-alpha")),
            Actor.Anonymous,
            Actor.Human(new ActorId("global-user-42"))
        );
        Assert.Throws<IdentifiedActorRequiredException>(() =>
            scope.ServiceProvider.GetRequiredService<IDraftOrderPreview>().Preview("WIDGET", 10)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SalesRejectsAnUnestablishedRequiredSegment(bool establishActor)
    {
        await using ServiceProvider provider = CreateProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        if (establishActor)
        {
            scope
                .ServiceProvider.GetRequiredService<IActorContextInitializer>()
                .Initialize(new ActorContext(Actor.Human(new ActorId("user"))));
        }
        else
        {
            scope
                .ServiceProvider.GetRequiredService<ITenantContextInitializer>()
                .Initialize(TenantContext.ForTenant(new TenantId("wholesale-alpha")));
        }

        Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IDraftOrderPreview>().Preview("WIDGET", 10)
        );
    }

    [Theory]
    [InlineData(false, ActorKind.Anonymous)]
    [InlineData(false, ActorKind.Human)]
    [InlineData(false, ActorKind.System)]
    [InlineData(true, ActorKind.Anonymous)]
    [InlineData(true, ActorKind.Human)]
    [InlineData(true, ActorKind.System)]
    public async Task TenantSelectionAndActorKindAreIndependentConsumerRequirements(
        bool selectTenant,
        ActorKind kind
    )
    {
        await using ServiceProvider provider = CreateProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        Actor actor = kind switch
        {
            ActorKind.Anonymous => Actor.Anonymous,
            ActorKind.Human => Actor.Human(new ActorId("user")),
            ActorKind.System => Actor.System(new ActorId("workflow")),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        Establish(
            scope,
            selectTenant
                ? TenantContext.ForTenant(new TenantId("wholesale-alpha"))
                : TenantContext.Tenantless(),
            actor
        );
        IDraftOrderPreview sales = scope.ServiceProvider.GetRequiredService<IDraftOrderPreview>();
        if (!selectTenant)
        {
            Assert.Throws<TenantRequiredException>(() => sales.Preview("WIDGET", 10));
        }
        else if (kind == ActorKind.Anonymous)
        {
            Assert.Throws<IdentifiedActorRequiredException>(() => sales.Preview("WIDGET", 10));
            Assert.Equal(
                42,
                scope
                    .ServiceProvider.GetRequiredService<IStockAvailability>()
                    .GetAvailableQuantity("WIDGET")
            );
        }
        else
        {
            Assert.Same(actor, sales.Preview("WIDGET", 10).RequestedBy);
        }
    }

    [Fact]
    public async Task ConcurrentOperationsKeepTheSameActorAndDifferentTenantsIsolated()
    {
        await using ServiceProvider provider = CreateProvider();
        var bothEstablished = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        Actor human = Actor.Human(new ActorId("global-user-42"));
        int established = 0;
        Task<DraftPreview> alpha = PreviewAsync("wholesale-alpha");
        Task<DraftPreview> beta = PreviewAsync("wholesale-beta");
        DraftPreview[] results = await Task.WhenAll(alpha, beta)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(42, results[0].AvailableQuantity);
        Assert.True(results[0].CanFulfil);
        Assert.Equal(7, results[1].AvailableQuantity);
        Assert.False(results[1].CanFulfil);
        Assert.All(results, preview => Assert.Same(human, preview.RequestedBy));

        async Task<DraftPreview> PreviewAsync(string tenant)
        {
            await using AsyncServiceScope scope = provider.CreateAsyncScope();
            Establish(scope, TenantContext.ForTenant(new TenantId(tenant)), human);
            if (Interlocked.Increment(ref established) == 2)
                bothEstablished.SetResult();
            await bothEstablished.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                TestContext.Current.CancellationToken
            );
            await Task.Yield();
            return scope
                .ServiceProvider.GetRequiredService<IDraftOrderPreview>()
                .Preview("WIDGET", 10);
        }
    }

    [Fact]
    public async Task IndependentChildScopeCannotInheritOrRebindEitherParentContext()
    {
        await using ServiceProvider provider = CreateProvider();
        await using AsyncServiceScope parent = provider.CreateAsyncScope();
        Establish(
            parent,
            TenantContext.ForTenant(new TenantId("wholesale-alpha")),
            Actor.Anonymous
        );
        await using (AsyncServiceScope child = parent.ServiceProvider.CreateAsyncScope())
        {
            Assert.Throws<InvalidOperationException>(() =>
                child.ServiceProvider.GetRequiredService<IActorContextAccessor>().Current
            );
            Assert.Throws<InvalidOperationException>(() =>
                child.ServiceProvider.GetRequiredService<ITenantContextAccessor>().Current
            );
            Establish(
                child,
                TenantContext.ForTenant(new TenantId("wholesale-beta")),
                Actor.System(new ActorId("child"))
            );
            Assert.Equal(
                7,
                child
                    .ServiceProvider.GetRequiredService<IStockAvailability>()
                    .GetAvailableQuantity("WIDGET")
            );
        }

        Assert.Throws<InvalidOperationException>(() =>
            parent
                .ServiceProvider.GetRequiredService<IActorContextInitializer>()
                .Initialize(new ActorContext(Actor.System(new ActorId("replacement"))))
        );
        Assert.Throws<InvalidOperationException>(() =>
            parent
                .ServiceProvider.GetRequiredService<ITenantContextInitializer>()
                .Initialize(TenantContext.ForTenant(new TenantId("wholesale-beta")))
        );
        Assert.Same(
            Actor.Anonymous,
            parent.ServiceProvider.GetRequiredService<IActorContextAccessor>().Current.Actor
        );
        Assert.Equal(
            42,
            parent
                .ServiceProvider.GetRequiredService<IStockAvailability>()
                .GetAvailableQuantity("WIDGET")
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExceptionOrCancellationDisposesBothAndNextOperationStartsFresh(bool cancel)
    {
        await using ServiceProvider provider = CreateProvider();
        IActorContextAccessor? previousActor = null;
        ITenantContextAccessor? previousTenant = null;
        using var cancellation = new CancellationTokenSource();
        Task failedOperation = RunFailureAsync();
        if (cancel)
            await Assert.ThrowsAsync<OperationCanceledException>(() => failedOperation);
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => failedOperation);
        Assert.NotNull(previousActor);
        Assert.NotNull(previousTenant);
        Assert.Throws<ObjectDisposedException>(() => previousActor.Current);
        Assert.Throws<ObjectDisposedException>(() => previousTenant.Current);
        await using AsyncServiceScope next = provider.CreateAsyncScope();
        Assert.Throws<InvalidOperationException>(() =>
            next.ServiceProvider.GetRequiredService<IActorContextAccessor>().Current
        );
        Assert.Throws<InvalidOperationException>(() =>
            next.ServiceProvider.GetRequiredService<ITenantContextAccessor>().Current
        );
        Establish(next, TenantContext.ForTenant(new TenantId("wholesale-beta")), Actor.Anonymous);
        Assert.Null(
            next.ServiceProvider.GetRequiredService<IActorContextAccessor>().Current.Initiator
        );
        Assert.Equal(
            7,
            next.ServiceProvider.GetRequiredService<IStockAvailability>()
                .GetAvailableQuantity("WIDGET")
        );

        async Task RunFailureAsync()
        {
            await using AsyncServiceScope scope = provider.CreateAsyncScope();
            Establish(
                scope,
                TenantContext.ForTenant(new TenantId("wholesale-alpha")),
                Actor.System(new ActorId("workflow")),
                Actor.Human(new ActorId("initiator"))
            );
            previousActor = scope.ServiceProvider.GetRequiredService<IActorContextAccessor>();
            previousTenant = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            Assert.Equal(
                42,
                scope
                    .ServiceProvider.GetRequiredService<IStockAvailability>()
                    .GetAvailableQuantity("WIDGET")
            );
            await Task.Yield();
            if (cancel)
            {
                cancellation.Cancel();
                cancellation.Token.ThrowIfCancellationRequested();
            }
            throw new InvalidOperationException("Consumer operation failed.");
        }
    }

    // Consumer-owned composition, not a combined library context or registration protocol.
    private static void Establish(
        AsyncServiceScope scope,
        TenantContext tenant,
        Actor actor,
        Actor? initiator = null
    )
    {
        scope
            .ServiceProvider.GetRequiredService<IActorContextInitializer>()
            .Initialize(new ActorContext(actor, initiator));
        scope.ServiceProvider.GetRequiredService<ITenantContextInitializer>().Initialize(tenant);
    }

    private static ServiceProvider CreateProvider() => Build(DemoComposition.CreateServices());

    private static ServiceProvider Build(ServiceCollection services) =>
        services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }
        );
}
