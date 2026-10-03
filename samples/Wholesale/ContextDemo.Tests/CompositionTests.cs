using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.ExecutionIdentity;
using ModulithFoundry.Samples.Wholesale.ContextDemo.Inventory.Contracts;
using ModulithFoundry.Samples.Wholesale.ContextDemo.Sales.Contracts;

namespace ModulithFoundry.Samples.Wholesale.ContextDemo.Tests;

public sealed class CompositionTests
{
    [Fact]
    public async Task ActualRegistrationSharesOneHolderAndDisposesAllAliasesSafely()
    {
        await using ServiceProvider provider = CreateProvider();
        IOperationContextAccessor reader;
        IOperationContextInitializer initializer;
        OperationContext captured;
        await using (AsyncServiceScope scope = provider.CreateAsyncScope())
        {
            reader = scope.ServiceProvider.GetRequiredService<IOperationContextAccessor>();
            initializer = scope.ServiceProvider.GetRequiredService<IOperationContextInitializer>();
            Assert.Same(reader, initializer);
            initializer.Initialize(
                OperationContext.ForTenant(new TenantId("wholesale-alpha"), Actor.Anonymous)
            );
            captured = reader.Current;
            Assert.Equal(
                42,
                scope
                    .ServiceProvider.GetRequiredService<IStockAvailability>()
                    .GetAvailableQuantity("WIDGET")
            );
        }

        Assert.Throws<ObjectDisposedException>(() => reader.Current);
        Assert.Throws<ObjectDisposedException>(() => initializer.Initialize(captured));
        Assert.Equal("wholesale-alpha", captured.RequireTenant().Value);
    }

    [Theory]
    [InlineData("wholesale-alpha", 42)]
    [InlineData("wholesale-beta", 7)]
    [InlineData("unknown-tenant", 0)]
    [InlineData("WHOLESALE-ALPHA", 0)]
    public async Task AnonymousAvailabilityUsesOnlyTheSelectedTenant(string tenant, int expected)
    {
        await using ServiceProvider provider = CreateProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        scope
            .ServiceProvider.GetRequiredService<IOperationContextInitializer>()
            .Initialize(OperationContext.ForTenant(new TenantId(tenant), Actor.Anonymous));

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
    public async Task SalesCallsInventoryThroughItsContractWithTheSameTenant(
        string tenant,
        int expected,
        bool canFulfil
    )
    {
        await using ServiceProvider provider = CreateProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        Actor human = Actor.Human(new ActorId("global-user-42"));
        scope
            .ServiceProvider.GetRequiredService<IOperationContextInitializer>()
            .Initialize(OperationContext.ForTenant(new TenantId(tenant), human));

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
        scope
            .ServiceProvider.GetRequiredService<IOperationContextInitializer>()
            .Initialize(
                OperationContext.ForTenant(new TenantId("wholesale-beta"), workflow, initiator)
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
            .ServiceProvider.GetRequiredService<IOperationContextInitializer>()
            .Initialize(OperationContext.Tenantless(Actor.System(new ActorId("maintenance"))));
        var failure = Assert.Throws<ContextRequirementException>(() =>
            stock.GetAvailableQuantity("WIDGET")
        );
        Assert.Equal(ContextRequirement.TenantRequired, failure.Requirement);
    }

    [Fact]
    public async Task HumanInitiatorCannotMakeAnAnonymousActorEligibleForSales()
    {
        await using ServiceProvider provider = CreateProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        scope
            .ServiceProvider.GetRequiredService<IOperationContextInitializer>()
            .Initialize(
                OperationContext.ForTenant(
                    new TenantId("wholesale-alpha"),
                    Actor.Anonymous,
                    Actor.Human(new ActorId("global-user-42"))
                )
            );

        var failure = Assert.Throws<ContextRequirementException>(() =>
            scope.ServiceProvider.GetRequiredService<IDraftOrderPreview>().Preview("WIDGET", 10)
        );
        Assert.Equal(ContextRequirement.IdentifiedActorRequired, failure.Requirement);
    }

    [Fact]
    public async Task ConcurrentOperationsRemainIsolatedAcrossAwaitAndBusinessCalls()
    {
        await using ServiceProvider provider = CreateProvider();
        var bothEstablished = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        int established = 0;
        Task<DraftPreview> alpha = PreviewAsync("wholesale-alpha", "alpha-user");
        Task<DraftPreview> beta = PreviewAsync("wholesale-beta", "beta-user");

        DraftPreview[] results = await Task.WhenAll(alpha, beta)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(42, results[0].AvailableQuantity);
        Assert.True(results[0].CanFulfil);
        Assert.Equal("alpha-user", results[0].RequestedBy.Id!.Value);
        Assert.Equal(7, results[1].AvailableQuantity);
        Assert.False(results[1].CanFulfil);
        Assert.Equal("beta-user", results[1].RequestedBy.Id!.Value);

        async Task<DraftPreview> PreviewAsync(string tenant, string actor)
        {
            await using AsyncServiceScope scope = provider.CreateAsyncScope();
            scope
                .ServiceProvider.GetRequiredService<IOperationContextInitializer>()
                .Initialize(
                    OperationContext.ForTenant(
                        new TenantId(tenant),
                        Actor.Human(new ActorId(actor))
                    )
                );
            if (Interlocked.Increment(ref established) == 2)
            {
                bothEstablished.SetResult();
            }

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
    public async Task IndependentChildScopeNeedsEstablishmentAndCannotRebindItsParent()
    {
        await using ServiceProvider provider = CreateProvider();
        await using AsyncServiceScope parent = provider.CreateAsyncScope();
        parent
            .ServiceProvider.GetRequiredService<IOperationContextInitializer>()
            .Initialize(
                OperationContext.ForTenant(new TenantId("wholesale-alpha"), Actor.Anonymous)
            );
        await using (AsyncServiceScope child = parent.ServiceProvider.CreateAsyncScope())
        {
            IOperationContextAccessor childReader =
                child.ServiceProvider.GetRequiredService<IOperationContextAccessor>();
            Assert.Throws<InvalidOperationException>(() => childReader.Current);
            child
                .ServiceProvider.GetRequiredService<IOperationContextInitializer>()
                .Initialize(
                    OperationContext.ForTenant(new TenantId("wholesale-beta"), Actor.Anonymous)
                );
            Assert.Equal(
                7,
                child
                    .ServiceProvider.GetRequiredService<IStockAvailability>()
                    .GetAvailableQuantity("WIDGET")
            );
        }

        IOperationContextInitializer parentInitializer =
            parent.ServiceProvider.GetRequiredService<IOperationContextInitializer>();
        Assert.Throws<InvalidOperationException>(() =>
            parentInitializer.Initialize(
                OperationContext.ForTenant(new TenantId("wholesale-beta"), Actor.Anonymous)
            )
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
    public async Task ExceptionOrCancellationDisposesScopeAndNextOperationStartsFresh(bool cancel)
    {
        await using ServiceProvider provider = CreateProvider();
        IOperationContextAccessor? previous = null;
        using var cancellation = new CancellationTokenSource();
        Task failedOperation = RunFailureAsync();
        if (cancel)
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() => failedOperation);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => failedOperation);
        }

        Assert.NotNull(previous);
        Assert.Throws<ObjectDisposedException>(() => previous.Current);
        await using AsyncServiceScope next = provider.CreateAsyncScope();
        IOperationContextAccessor reader =
            next.ServiceProvider.GetRequiredService<IOperationContextAccessor>();
        Assert.Throws<InvalidOperationException>(() => reader.Current);
        next.ServiceProvider.GetRequiredService<IOperationContextInitializer>()
            .Initialize(
                OperationContext.ForTenant(new TenantId("wholesale-beta"), Actor.Anonymous)
            );
        Assert.Null(reader.Current.Initiator);
        Assert.Equal(
            7,
            next.ServiceProvider.GetRequiredService<IStockAvailability>()
                .GetAvailableQuantity("WIDGET")
        );

        async Task RunFailureAsync()
        {
            await using AsyncServiceScope scope = provider.CreateAsyncScope();
            scope
                .ServiceProvider.GetRequiredService<IOperationContextInitializer>()
                .Initialize(
                    OperationContext.ForTenant(
                        new TenantId("wholesale-alpha"),
                        Actor.System(new ActorId("workflow")),
                        Actor.Human(new ActorId("initiator"))
                    )
                );
            previous = scope.ServiceProvider.GetRequiredService<IOperationContextAccessor>();
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

    private static ServiceProvider CreateProvider() =>
        DemoComposition
            .CreateServices()
            .BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }
            );
}
