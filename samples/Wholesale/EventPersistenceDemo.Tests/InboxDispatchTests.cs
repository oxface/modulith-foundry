using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Samples.Wholesale.Inventory;
using ModulithFoundry.Samples.Wholesale.Inventory.Contracts;
using ModulithFoundry.Tests.Infrastructure;
using Npgsql;
using Rootbolt.Messaging;
using Rootbolt.Messaging.EntityFrameworkCore;
using Rootbolt.Tenancy;

namespace ModulithFoundry.Samples.Wholesale.EventPersistenceDemo.Tests;

public sealed class InboxDispatchTests(PostgreSqlFixture postgres, RabbitMqFixture rabbit)
    : IClassFixture<PostgreSqlFixture>,
        IClassFixture<RabbitMqFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private const string Alpha = "wholesale-alpha";
    private const string Beta = "wholesale-beta";
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("accepted")]
    [InlineData("shortage")]
    [InlineData("conflict")]
    [InlineData("missing")]
    public async Task AdmittedCommandAtomicallyCompletesWithStockFactsOrAnExplicitBusinessRefusal(
        string decision
    )
    {
        string connection = await DatabaseAsync();
        await using var provider = Provider(connection);
        Guid stock = await SeedAsync(provider, Alpha);
        var request = Message(
            decision == "missing" ? Guid.NewGuid() : stock,
            decision == "conflict" ? 7 : 2,
            decision == "shortage" ? 6 : 2
        );
        await IntakeAsync(provider, request);
        Assert.Equal(InboxProcessingResult.Processed, await ProcessAsync(provider));
        await using var read = Scope(provider, Alpha);
        var database = read.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var state = await read
            .ServiceProvider.GetRequiredService<IStockPositionQueries>()
            .ReadCurrentAsync(stock, Token);
        Assert.NotNull(state);
        bool accepted = decision == "accepted";
        Assert.Equal(accepted ? 3 : 2, state.Version);
        Assert.Equal(accepted ? 3m : 5m, state.OnHand);
        Assert.NotNull((await database.Set<InboxMessageRecord>().SingleAsync(Token)).ProcessedAt);
        var reply = Assert.Single(await database.Set<OutboxMessageRecord>().ToArrayAsync(Token));
        Assert.Equal(Alpha, reply.TenantKey);
        Assert.Equal(request.CorrelationId, reply.CorrelationId);
        Assert.Equal(request.MessageId.ToString(), reply.CausationId);
        Assert.Equal(
            accepted ? "inventory.stock-issue-recorded" : "inventory.stock-issue-declined",
            reply.MessageName
        );
        if (!accepted)
            Assert.Equal(
                decision switch
                {
                    "shortage" => (int)StockIssueDeclineReason.InsufficientStock,
                    "conflict" => (int)StockIssueDeclineReason.Conflict,
                    _ => (int)StockIssueDeclineReason.NotFound,
                },
                reply.Payload.GetProperty("reason").GetInt32()
            );
        Assert.Equal(InboxReceiveResult.AlreadyReceived, await IntakeAsync(provider, request));
        Assert.Equal(InboxProcessingResult.NoWork, await ProcessAsync(provider));
        Assert.False(database.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData("reply")]
    [InlineData("completion")]
    public async Task FailureAfterAcceptedDecisionRollsBackFactsInlineStateReplyAndCompletionThenFreshScopeRecovers(
        string fault
    )
    {
        string connection = await DatabaseAsync();
        await using var provider = Provider(connection);
        Guid stock = await SeedAsync(provider, Alpha);
        await IntakeAsync(provider, Message(stock, 2, 2));
        await SqlAsync(
            connection,
            fault == "reply"
                ? "ALTER TABLE inventory.outbox_messages ADD CONSTRAINT proof_reply CHECK (schema_version > 1)"
                : "ALTER TABLE inventory.inbox_messages ADD CONSTRAINT proof_complete CHECK (processed_at IS NULL)"
        );
        await Assert.ThrowsAnyAsync<Exception>(() => ProcessAsync(provider));
        await using (var read = Scope(provider, Alpha))
        {
            var database = read.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var state = await read
                .ServiceProvider.GetRequiredService<IStockPositionQueries>()
                .ReadCurrentAsync(stock, Token);
            Assert.Equal(2, state!.Version);
            Assert.Equal(5m, state.OnHand);
            Assert.Empty(await database.Set<OutboxMessageRecord>().ToArrayAsync(Token));
            Assert.Null((await database.Set<InboxMessageRecord>().SingleAsync(Token)).ProcessedAt);
        }
        await SqlAsync(
            connection,
            fault == "reply"
                ? "ALTER TABLE inventory.outbox_messages DROP CONSTRAINT proof_reply"
                : "ALTER TABLE inventory.inbox_messages DROP CONSTRAINT proof_complete"
        );
        await SqlAsync(
            connection,
            "UPDATE inventory.inbox_messages SET available_at = clock_timestamp() - interval '1 second'"
        );
        Assert.Equal(InboxProcessingResult.Processed, await ProcessAsync(provider));
        await using var recovered = Scope(provider, Alpha);
        var current = await recovered
            .ServiceProvider.GetRequiredService<IStockPositionQueries>()
            .ReadCurrentAsync(stock, Token);
        Assert.Equal(3, current!.Version);
        Assert.Equal(3m, current.OnHand);
        Assert.Single(
            await recovered
                .ServiceProvider.GetRequiredService<InventoryDbContext>()
                .Set<OutboxMessageRecord>()
                .ToArrayAsync(Token)
        );
    }

    [Fact]
    public async Task FreshProcessingScopesKeepTwoAdmittedOrganizationsIndependentAndRejectUnknownMetadata()
    {
        string connection = await DatabaseAsync();
        await using var provider = Provider(connection);
        Guid alpha = await SeedAsync(provider, Alpha);
        Guid beta = await SeedAsync(provider, Beta);
        await IntakeAsync(provider, Message(alpha, 2, 1));
        await IntakeAsync(provider, Message(beta, 2, 2, Beta));
        Assert.Equal(InboxProcessingResult.Processed, await ProcessAsync(provider));
        Assert.Equal(InboxProcessingResult.Processed, await ProcessAsync(provider));
        foreach (string owner in new[] { Alpha, Beta })
        {
            await using var scope = Scope(provider, owner);
            var database = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            Assert.Equal(
                owner,
                (await database.Set<InboxMessageRecord>().SingleAsync(Token)).TenantKey
            );
            Assert.Equal(
                owner,
                (await database.Set<OutboxMessageRecord>().SingleAsync(Token)).TenantKey
            );
        }
        var denied = Message(alpha, 3, 1, "unadmitted");
        Assert.Throws<InvalidDataException>(() => StockIssueMessageAdmission.Validate(denied));
        // Even incorrectly retained technical intake cannot grant business admission during processing.
        await IntakeAsync(provider, denied);
        await Assert.ThrowsAsync<InvalidDataException>(() => ProcessAsync(provider));
        await using var read = Scope(provider, Alpha);
        Assert.Equal(
            4m,
            (
                await read
                    .ServiceProvider.GetRequiredService<IStockPositionQueries>()
                    .ReadCurrentAsync(alpha, Token)
            )!.OnHand
        );
    }

    [Fact]
    public async Task NativeBrokerJourneyExercisesTheOptInInventoryReceiver()
    {
        string connection = await postgres.CreateDatabaseAsync(Token);
        using var output = new StringWriter();
        await InboxJourney.RunAsync(connection, new Uri(rabbit.ConnectionString), output, Token);
        Assert.Contains("processing=Processed", output.ToString());
        await using var provider = Provider(connection);
        await using var scope = Scope(provider, Alpha);
        var database = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var incoming = await database.Set<InboxMessageRecord>().SingleAsync(Token);
        var reply = await database.Set<OutboxMessageRecord>().SingleAsync(Token);
        Assert.NotNull(incoming.ProcessedAt);
        Assert.Equal(incoming.MessageId.ToString(), reply.CausationId);
        Assert.Equal(incoming.CorrelationId, reply.CorrelationId);
    }

    private async Task<string> DatabaseAsync()
    {
        string connection = await postgres.CreateDatabaseAsync(Token);
        await using var provider = Provider(connection);
        await using var scope = Scope(provider, Alpha);
        await scope
            .ServiceProvider.GetRequiredService<InventoryDbContext>()
            .Database.MigrateAsync(Token);
        return connection;
    }

    private static ServiceProvider Provider(string connection)
    {
        var services = DemoComposition.CreateServices(connection);
        services.AddStockIssueInbox();
        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );
    }

    private static IncomingMessage Message(
        Guid stock,
        long version,
        decimal quantity,
        string owner = Alpha
    ) =>
        new(
            Guid.NewGuid(),
            StockIssueMessageAdmission.Producer,
            StockIssueMessageAdmission.Subscription,
            1,
            JsonSerializer.SerializeToElement(new IssueStockV1(stock, version, quantity), WireJson),
            owner,
            "issue-conversation",
            "earlier-request"
        );

    private static AsyncServiceScope Scope(ServiceProvider provider, string owner)
    {
        var scope = provider.CreateAsyncScope();
        scope
            .ServiceProvider.GetRequiredService<ITenantContextInitializer>()
            .Initialize(TenantContext.ForTenant(new TenantId(owner)));
        return scope;
    }

    private static async Task<Guid> SeedAsync(ServiceProvider provider, string owner)
    {
        Guid stock = Guid.NewGuid();
        await using (var scope = Scope(provider, owner))
        {
            var database = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await using var transaction = await database.Database.BeginTransactionAsync(Token);
            Assert.IsType<StockPositionChangeResult.Changed>(
                await scope
                    .ServiceProvider.GetRequiredService<IStockPositionCommands>()
                    .OpenAsync(new(stock, Guid.NewGuid(), Guid.NewGuid(), "EA"), Token)
            );
            await database.SaveChangesAsync(Token);
            await transaction.CommitAsync(Token);
        }
        await using (var scope = Scope(provider, owner))
        {
            var database = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await using var transaction = await database.Database.BeginTransactionAsync(Token);
            Assert.IsType<StockPositionChangeResult.Changed>(
                await scope
                    .ServiceProvider.GetRequiredService<IStockPositionCommands>()
                    .ReceiveAsync(new(stock, 1, [new StockReceipt(5)]), Token)
            );
            await database.SaveChangesAsync(Token);
            await transaction.CommitAsync(Token);
        }
        return stock;
    }

    private static async Task<InboxReceiveResult> IntakeAsync(
        ServiceProvider provider,
        IncomingMessage message
    )
    {
        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(Token);
        var result = await scope
            .ServiceProvider.GetRequiredService<IInbox<InventoryDbContext>>()
            .ReceiveAsync(StockIssueMessageAdmission.Subscription, message, Token);
        await transaction.CommitAsync(Token);
        return result;
    }

    private static async Task<InboxProcessingResult> ProcessAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<IInboxProcessor<InventoryDbContext>>()
            .ProcessNextAsync(StockIssueMessageAdmission.Subscription, Token);
    }

    private static async Task SqlAsync(string connection, string sql)
    {
        await using var database = new NpgsqlConnection(connection);
        await database.OpenAsync(Token);
        await using var command = new NpgsqlCommand(sql, database);
        await command.ExecuteNonQueryAsync(Token);
    }
}
