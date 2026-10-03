using System.Diagnostics.CodeAnalysis;
using ModulithFoundry.ExecutionIdentity;

namespace ModulithFoundry.ContextTests;

[SuppressMessage(
    "Performance",
    "CA1859:Use concrete types when possible",
    Justification = "These tests exercise the public reader and initializer interfaces."
)]
public sealed class AccessorTests
{
    [Fact]
    public void UnestablishedContextNeverFallsBackToAnonymous()
    {
        using var holder = new OperationContextAccessor();
        IOperationContextAccessor reader = holder;
        IOperationContextInitializer initializer = holder;

        Assert.Throws<InvalidOperationException>(() => reader.Current);
        Assert.Throws<ArgumentNullException>(() => initializer.Initialize(null!));
        Assert.Throws<InvalidOperationException>(() => reader.Current);

        OperationContext deliberate = OperationContext.Tenantless(Actor.Anonymous);
        initializer.Initialize(deliberate);
        Assert.Same(deliberate, reader.Current);
    }

    [Fact]
    public void SameOrDifferentContextCannotReplaceTheEstablishedValue()
    {
        using var holder = new OperationContextAccessor();
        IOperationContextAccessor reader = holder;
        IOperationContextInitializer initializer = holder;
        OperationContext first = OperationContext.ForTenant(new TenantId("alpha"), Actor.Anonymous);
        initializer.Initialize(first);

        Assert.Throws<InvalidOperationException>(() => initializer.Initialize(first));
        Assert.Throws<InvalidOperationException>(() =>
            initializer.Initialize(
                OperationContext.ForTenant(
                    new TenantId("beta"),
                    Actor.System(new ActorId("workflow"))
                )
            )
        );
        Assert.Same(first, reader.Current);
    }

    [Fact]
    public async Task CompetingInitializersPublishExactlyOneCompleteContext()
    {
        using var holder = new OperationContextAccessor();
        IOperationContextAccessor reader = holder;
        IOperationContextInitializer initializer = holder;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        OperationContext alpha = OperationContext.ForTenant(
            new TenantId("alpha"),
            Actor.System(new ActorId("alpha-workflow")),
            Actor.Human(new ActorId("alpha-user"))
        );
        OperationContext beta = OperationContext.ForTenant(
            new TenantId("beta"),
            Actor.System(new ActorId("beta-workflow")),
            Actor.Human(new ActorId("beta-user"))
        );

        Task<OperationContext?>[] attempts = [AttemptAsync(alpha), AttemptAsync(beta)];
        start.SetResult();
        OperationContext?[] results = await Task.WhenAll(attempts)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        OperationContext winner = Assert.Single(results.OfType<OperationContext>());
        Assert.Single(results, result => result is null);
        Assert.Same(winner, reader.Current);
        Assert.Equal(winner.Tenant, reader.Current.Tenant);
        Assert.Equal(winner.Actor, reader.Current.Actor);
        Assert.Equal(winner.Initiator, reader.Current.Initiator);

        async Task<OperationContext?> AttemptAsync(OperationContext context)
        {
            await start.Task.WaitAsync(TestContext.Current.CancellationToken);
            try
            {
                initializer.Initialize(context);
                return context;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }

    [Fact]
    public async Task ParallelReadersObserveTheSameCompleteImmutableValue()
    {
        using var holder = new OperationContextAccessor();
        IOperationContextAccessor reader = holder;
        OperationContext expected = OperationContext.ForTenant(
            new TenantId("alpha"),
            Actor.System(new ActorId("workflow")),
            Actor.Human(new ActorId("initiator"))
        );
        holder.Initialize(expected);

        Task[] reads = Enumerable
            .Range(0, 16)
            .Select(_ =>
                Task.Run(
                    () =>
                    {
                        for (int index = 0; index < 100; index++)
                        {
                            OperationContext actual = reader.Current;
                            Assert.Same(expected, actual);
                            Assert.Equal("alpha", actual.RequireTenant().Value);
                            Assert.Equal(ActorKind.System, actual.RequireIdentifiedActor().Kind);
                            Assert.Equal("initiator", actual.Initiator!.Id!.Value);
                        }
                    },
                    TestContext.Current.CancellationToken
                )
            )
            .ToArray();
        await Task.WhenAll(reads)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisposalIsIdempotentAndPreventsReadsOrReinitialization(bool initialize)
    {
        var holder = new OperationContextAccessor();
        IOperationContextAccessor reader = holder;
        IOperationContextInitializer initializer = holder;
        OperationContext context = OperationContext.Tenantless(Actor.Anonymous);
        if (initialize)
        {
            initializer.Initialize(context);
        }

        holder.Dispose();
        holder.Dispose();
        Assert.Throws<ObjectDisposedException>(() => reader.Current);
        Assert.Throws<ObjectDisposedException>(() => initializer.Initialize(context));
        Assert.Same(Actor.Anonymous, context.Actor);
    }
}
