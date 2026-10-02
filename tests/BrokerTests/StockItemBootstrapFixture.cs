using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Composition;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Purchasing.Composition;
using ModulithFoundry.Modules.Purchasing.Contracts;
using Npgsql;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Serialization.Custom;
using Rebus.Serialization.Json;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace ModulithFoundry.BrokerTests;

internal sealed class StockItemBootstrapFixture : IAsyncDisposable
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private readonly RabbitMqContainer rabbit = new RabbitMqBuilder(
        "rabbitmq:4.3.6-management"
    ).Build();
    private readonly CancellationTokenSource timeout =
        CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private NpgsqlDataSource dataSource = null!;
    private IHost host = null!;
    private readonly SnapshotReadBarrier snapshotBarrier = new();
    private readonly DeliveryProbe deliveries = new();
    private IHost? sender;
    private IHost? errorObserver;
    private IHost? createdObserver;
    private readonly Channel<ReplenishmentRequirementCreatedV1> createdRequirements =
        Channel.CreateUnbounded<ReplenishmentRequirementCreatedV1>();
    private readonly Channel<StockItemReferenceChangedV1> errors =
        Channel.CreateUnbounded<StockItemReferenceChangedV1>();
    private readonly Channel<CreateReplenishmentRequirementV1> requestErrors =
        Channel.CreateUnbounded<CreateReplenishmentRequirementV1>();
    internal string DatabaseConnectionString => postgres.GetConnectionString();
    internal string BrokerConnectionString => rabbit.GetConnectionString();
    internal OrganizationAccessContext Actor { get; } =
        new(
            new UserId(Guid.CreateVersion7()),
            new OrganizationId(Guid.CreateVersion7()),
            new MembershipId(Guid.CreateVersion7()),
            "Bootstrap",
            "bootstrap",
            []
        );
    internal CancellationToken CancellationToken => timeout.Token;
    internal IMeterFactory MeterFactory => host.Services.GetRequiredService<IMeterFactory>();

    internal static async Task<StockItemBootstrapFixture> StartAsync()
    {
        var fixture = new StockItemBootstrapFixture();
        fixture.timeout.CancelAfter(TimeSpan.FromSeconds(150));
        try
        {
            await fixture.postgres.StartAsync(fixture.CancellationToken);
            await fixture.rabbit.StartAsync(fixture.CancellationToken);
            fixture.dataSource = NpgsqlDataSource.Create(fixture.postgres.GetConnectionString());
            fixture.host = fixture.BuildHost();
            await fixture.host.Services.MigrateInventoryAsync(fixture.CancellationToken);
            await fixture.host.Services.MigratePurchasingAsync(fixture.CancellationToken);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    private bool started;

    private IHost BuildHost() =>
        Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
                logging
                    .SetMinimumLevel(LogLevel.Warning)
                    .AddProvider(snapshotBarrier)
                    .AddFilter<SnapshotReadBarrier>(
                        "Microsoft.EntityFrameworkCore.Database.Command",
                        LogLevel.Information
                    )
            )
            .ConfigureServices(services =>
            {
                services.AddSingleton(dataSource);
                services.AddSingleton<IOrganizationContextAccessor>(new TestActor(Actor));
                services.AddSingleton<IOrganizationAuthorization, AllowedAuthorization>();
                services.AddInventoryModule();
                services.AddPurchasingModule();
            })
            .AddPurchasingMessaging(
                Configuration(),
                options =>
                    options.Decorate<IPipeline>(context =>
                        new PipelineStepInjector(context.Get<IPipeline>()).OnReceive(
                            deliveries,
                            PipelineRelativePosition.Before,
                            typeof(DispatchIncomingMessageStep)
                        )
                    )
            )
            .AddInventoryMessaging(Configuration())
            .Build();

    private IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:rabbitmq"] = rabbit.GetConnectionString(),
                }
            )
            .Build();

    internal async Task StartEndpointsAsync()
    {
        if (started)
            return;
        await host.StartAsync(CancellationToken);
        started = true;
    }

    internal void PauseSnapshotAfterWatermark() => snapshotBarrier.Arm();

    internal Task WaitForSnapshotWatermarkAsync() =>
        snapshotBarrier.Entered.WaitAsync(CancellationToken);

    internal void ReleaseSnapshot() => snapshotBarrier.Release();

    internal async Task WaitForReadyAsync()
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            if ((await StatusAsync()).IsReady)
                return;
            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken);
        }
        Assert.Fail("Purchasing bootstrap did not become ready.");
    }

    internal async Task RestartAsync()
    {
        await host.StopAsync(CancellationToken);
        host.Dispose();
        host = BuildHost();
        started = false;
        await StartEndpointsAsync();
    }

    internal async Task StartProducerOnlyAsync()
    {
        if (started)
            await host.StopAsync(CancellationToken);
        host.Dispose();
        host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            .ConfigureServices(services =>
            {
                services.AddSingleton(dataSource);
                services.AddSingleton<IOrganizationContextAccessor>(new TestActor(Actor));
                services.AddSingleton<IOrganizationAuthorization, AllowedAuthorization>();
                services.AddInventoryModule();
                services.AddPurchasingModule();
            })
            .AddInventoryMessaging(Configuration())
            .Build();
        await host.StartAsync(CancellationToken);
        started = true;
    }

    internal async Task StartErrorObserverAsync()
    {
        errorObserver = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            .ConfigureServices(services =>
            {
                services.AddSingleton(errors);
                services.AddSingleton(requestErrors);
                services.AddRebusHandler<ReferenceErrorObserver>();
                services.AddRebusHandler<ReplenishmentErrorObserver>();
                services.AddRebus(configure =>
                    configure
                        .Transport(transport =>
                            transport.UseRabbitMq(
                                BrokerConnectionString,
                                PurchasingMessaging.ErrorQueue
                            )
                        )
                        .Serialization(serializer =>
                        {
                            serializer.UseSystemTextJson(
                                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                            );
                            serializer
                                .UseCustomMessageTypeNames()
                                .AddWithCustomName<StockItemReferenceChangedV1>(
                                    StockItemReferenceChangedV1.LogicalName
                                )
                                .AddWithCustomName<CreateReplenishmentRequirementV1>(
                                    CreateReplenishmentRequirementV1.LogicalName
                                );
                        })
                );
            })
            .Build();
        await errorObserver.StartAsync(CancellationToken);
    }

    internal ValueTask<StockItemReferenceChangedV1> ReadErrorAsync() =>
        errors.Reader.ReadAsync(CancellationToken);

    internal ValueTask<CreateReplenishmentRequirementV1> ReadRequestErrorAsync() =>
        requestErrors.Reader.ReadAsync(CancellationToken);

    internal async Task SendAsync(StockItemReferenceChangedV1 message)
    {
        await EnsureSenderAsync();
        await sender!
            .Services.GetRequiredService<IBus>()
            .Advanced.Routing.Send(
                PurchasingMessaging.InputQueue,
                message,
                new Dictionary<string, string>
                {
                    [Headers.MessageId] = message.MessageId.ToString(),
                    ["producer-module"] = "inventory",
                }
            );
    }

    internal async Task SendAsync(
        CreateReplenishmentRequirementV1 command,
        string producer = "sales",
        Guid? correlationId = null
    )
    {
        // Initialize the existing test publisher without creating a business effect.
        await EnsureSenderAsync();
        await sender!
            .Services.GetRequiredService<IBus>()
            .Advanced.Routing.Send(
                PurchasingMessaging.InputQueue,
                command,
                new Dictionary<string, string>
                {
                    [Headers.MessageId] = command.MessageId.ToString(),
                    ["producer-module"] = producer,
                    [Headers.CorrelationId] = (correlationId ?? command.ProcessId).ToString(),
                }
            );
    }

    private async Task EnsureSenderAsync()
    {
        if (sender is not null)
            return;
        sender = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            .ConfigureServices(services =>
                services.AddRebus(configure =>
                    configure
                        .Transport(transport =>
                            transport.UseRabbitMq(
                                BrokerConnectionString,
                                "test.replenishment-publisher"
                            )
                        )
                        .Serialization(serializer =>
                        {
                            serializer.UseSystemTextJson(
                                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                            );
                            serializer
                                .UseCustomMessageTypeNames()
                                .AddWithCustomName<StockItemReferenceChangedV1>(
                                    StockItemReferenceChangedV1.LogicalName
                                )
                                .AddWithCustomName<CreateReplenishmentRequirementV1>(
                                    CreateReplenishmentRequirementV1.LogicalName
                                );
                        })
                )
            )
            .Build();
        await sender.StartAsync(CancellationToken);
    }

    internal async Task<ReplenishmentRequestView?> RequestAsync(
        Guid operationId,
        OrganizationId? organizationId = null
    )
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<IReplenishmentRequestQueries>()
            .GetAsync(organizationId ?? Actor.OrganizationId, operationId, CancellationToken);
    }

    internal async Task<GetReplenishmentRequirementResult> RequirementAsync(
        long number,
        OrganizationId? organizationId = null
    )
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<IReplenishmentRequirementQueries>()
            .GetByNumberAsync(
                Actor.UserId,
                organizationId ?? Actor.OrganizationId,
                number,
                CancellationToken
            );
    }

    internal async Task<ListReplenishmentRequirementsResult> RequirementsAsync()
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<IReplenishmentRequirementQueries>()
            .ListAsync(Actor.UserId, Actor.OrganizationId, 0, 25, CancellationToken);
    }

    internal async Task StartCreatedObserverAsync()
    {
        createdObserver = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            .ConfigureServices(services =>
            {
                services.AddSingleton(createdRequirements);
                services.AddRebusHandler<RequirementCreatedObserver>();
                services.AddRebus(configure =>
                    configure
                        .Transport(transport =>
                            transport.UseRabbitMq(
                                BrokerConnectionString,
                                "test.requirement-created"
                            )
                        )
                        .Serialization(serializer =>
                        {
                            serializer.UseSystemTextJson(
                                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                            );
                            serializer
                                .UseCustomMessageTypeNames()
                                .AddWithCustomName<ReplenishmentRequirementCreatedV1>(
                                    ReplenishmentRequirementCreatedV1.LogicalName
                                );
                        })
                );
            })
            .Build();
        await createdObserver.StartAsync(CancellationToken);
        await createdObserver
            .Services.GetRequiredService<IBus>()
            .Advanced.Topics.Subscribe(ReplenishmentRequirementCreatedV1.LogicalName);
    }

    internal ValueTask<ReplenishmentRequirementCreatedV1> ReadCreatedAsync() =>
        createdRequirements.Reader.ReadAsync(CancellationToken);

    internal Task WaitForProcessedAsync(Guid messageId, int count = 1) =>
        deliveries.WaitAsync(messageId, count, CancellationToken);

    internal async Task ExecuteSqlAsync(string sql)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(
            CancellationToken
        );
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken);
    }

    internal async Task PublishHistoryBeforePurchasingSubscribesAsync()
    {
        using IHost producer = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            .ConfigureServices(services =>
            {
                services.AddSingleton(dataSource);
                services.AddSingleton<IOrganizationContextAccessor>(new TestActor(Actor));
                services.AddSingleton<IOrganizationAuthorization, AllowedAuthorization>();
                services.AddInventoryModule();
            })
            .AddInventoryMessaging(Configuration())
            .Build();
        await producer.StartAsync(CancellationToken);
        try
        {
            // Coordination only: let the real relay complete historical publications
            // before adding the consumer. No projection/business assertion reads SQL.
            for (int attempt = 0; attempt < 100; attempt++)
            {
                await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(
                    CancellationToken
                );
                await using var command = new NpgsqlCommand(
                    "SELECT count(*) FROM inventory.outbox_messages WHERE dispatched_at IS NULL",
                    connection
                );
                if ((long)(await command.ExecuteScalarAsync(CancellationToken))! == 0)
                    return;
                await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken);
            }
            throw new TimeoutException(
                "Historical Inventory publications did not drain before bootstrap setup."
            );
        }
        finally
        {
            await producer.StopAsync(CancellationToken.None);
        }
    }

    internal async Task WaitForDescriptionAsync(StockItemId id, string description)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if ((await GetAsync(id))?.Description == description)
                return;
            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken);
        }
        Assert.Fail($"Purchasing did not reach description '{description}'.");
    }

    internal async Task ChangeDescriptionAsync(string sku, string description)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        Assert.IsType<ChangeStockItemDescriptionResult.Changed>(
            await scope
                .ServiceProvider.GetRequiredService<IStockItemAdministration>()
                .ChangeDescriptionAsync(
                    new(Actor.UserId, Actor.OrganizationId, sku, description),
                    CancellationToken
                )
        );
    }

    internal async Task<StockItemId> CreateAsync(string sku)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        return Assert
            .IsType<CreateStockItemResult.Created>(
                await scope
                    .ServiceProvider.GetRequiredService<IStockItemAdministration>()
                    .CreateAsync(
                        new(Actor.UserId, Actor.OrganizationId, sku, "Bolt", "EA"),
                        CancellationToken
                    )
            )
            .Item.StockItemId;
    }

    internal async Task SetActiveAsync(string sku, bool isActive)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        Assert.IsType<SetStockItemActiveResult.Changed>(
            await scope
                .ServiceProvider.GetRequiredService<IStockItemAdministration>()
                .SetActiveAsync(
                    new(Actor.UserId, Actor.OrganizationId, sku, isActive),
                    CancellationToken
                )
        );
    }

    internal async Task BootstrapAsync()
    {
        await StartEndpointsAsync();
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await scope
            .ServiceProvider.GetRequiredService<IStockItemProjectionBootstrapper>()
            .EnsureInitializedAsync(CancellationToken);
    }

    internal async Task<StockItemSnapshotV1> ExportAsync()
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<IStockItemSnapshotExporter>()
            .ExportAsync(CancellationToken);
    }

    internal async Task<StockItemProjectionComparison> CompareAsync(bool repair = false)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        var reconciliation =
            scope.ServiceProvider.GetRequiredService<IStockItemProjectionReconciliation>();
        return repair
            ? await reconciliation.RepairAsync(CancellationToken)
            : await reconciliation.InspectAsync(CancellationToken);
    }

    internal async Task InterleaveCreatesInReusedScopeAsync()
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IStockItemAdministration administration =
            scope.ServiceProvider.GetRequiredService<IStockItemAdministration>();
        Assert.IsType<CreateStockItemResult.Created>(
            await administration.CreateAsync(
                new(Actor.UserId, Actor.OrganizationId, "BOLT", "Bolt", "EA"),
                CancellationToken
            )
        );
        await CreateAsync("NUT");
        Assert.IsType<CreateStockItemResult.Created>(
            await administration.CreateAsync(
                new(Actor.UserId, Actor.OrganizationId, "SCREW", "Screw", "EA"),
                CancellationToken
            )
        );
    }

    internal async Task<StockItemId> InterleaveChangesInReusedScopeAsync()
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IStockItemAdministration administration =
            scope.ServiceProvider.GetRequiredService<IStockItemAdministration>();
        StockItemId id = Assert
            .IsType<CreateStockItemResult.Created>(
                await administration.CreateAsync(
                    new(Actor.UserId, Actor.OrganizationId, "BOLT", "Bolt", "EA"),
                    CancellationToken
                )
            )
            .Item.StockItemId;
        await BootstrapAsync();
        await ChangeDescriptionAsync("BOLT", "Fresh description");
        Assert.IsType<SetStockItemActiveResult.Changed>(
            await administration.SetActiveAsync(
                new(Actor.UserId, Actor.OrganizationId, "BOLT", false),
                CancellationToken
            )
        );
        return id;
    }

    internal async Task<StockItemProjectionView?> GetAsync(StockItemId id)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<IStockItemProjectionQueries>()
            .GetAsync(Actor.OrganizationId, id.Value, CancellationToken);
    }

    internal async Task<StockItemProjectionView?> GetAsync(OrganizationId organizationId, Guid id)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<IStockItemProjectionQueries>()
            .GetAsync(organizationId, id, CancellationToken);
    }

    internal async Task<StockItemProjectionStatus> StatusAsync()
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        return await scope
            .ServiceProvider.GetRequiredService<IStockItemProjectionQueries>()
            .GetStatusAsync(CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        snapshotBarrier.Release();
        if (host is not null)
        {
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
        }
        if (sender is not null)
        {
            await sender.StopAsync(CancellationToken.None);
            sender.Dispose();
        }
        if (errorObserver is not null)
        {
            await errorObserver.StopAsync(CancellationToken.None);
            errorObserver.Dispose();
        }
        if (createdObserver is not null)
        {
            await createdObserver.StopAsync(CancellationToken.None);
            createdObserver.Dispose();
        }
        if (dataSource is not null)
            await dataSource.DisposeAsync();
        await rabbit.DisposeAsync();
        await postgres.DisposeAsync();
        timeout.Dispose();
        snapshotBarrier.Dispose();
    }

    private sealed class TestActor(OrganizationAccessContext actor) : IOrganizationContextAccessor
    {
        public OrganizationAccessContext? OrganizationContext => actor;
    }

    private sealed class AllowedAuthorization : IOrganizationAuthorization
    {
        public Task<bool> HasPermissionAsync(
            UserId userId,
            OrganizationId organizationId,
            string permissionId,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(true);
    }
}

internal sealed class ReferenceErrorObserver(Channel<StockItemReferenceChangedV1> errors)
    : IHandleMessages<StockItemReferenceChangedV1>
{
    public Task Handle(StockItemReferenceChangedV1 message) =>
        errors.Writer.WriteAsync(message).AsTask();
}

internal sealed class ReplenishmentErrorObserver(Channel<CreateReplenishmentRequirementV1> errors)
    : IHandleMessages<CreateReplenishmentRequirementV1>
{
    public Task Handle(CreateReplenishmentRequirementV1 message) =>
        errors.Writer.WriteAsync(message).AsTask();
}

internal sealed class RequirementCreatedObserver(Channel<ReplenishmentRequirementCreatedV1> created)
    : IHandleMessages<ReplenishmentRequirementCreatedV1>
{
    public Task Handle(ReplenishmentRequirementCreatedV1 message)
    {
        var headers = MessageContext.Current.Headers;
        Assert.Equal(message.MessageId.ToString(), headers[Headers.MessageId]);
        Assert.Equal(message.CausationId.ToString(), headers["causation-id"]);
        Assert.Equal("purchasing", headers["producer-module"]);
        return created.Writer.WriteAsync(message).AsTask();
    }
}

// A test-only command-completion barrier. It pauses a real repeatable-read export after
// PostgreSQL establishes its snapshot, without replacing the exporter or its data.
internal sealed class SnapshotReadBarrier : ILoggerProvider
{
    private readonly TaskCompletionSource entered = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly ManualResetEventSlim release = new(true);
    private int armed;
    internal Task Entered => entered.Task;

    internal void Arm()
    {
        release.Reset();
        Volatile.Write(ref armed, 1);
    }

    internal void Release() => release.Set();

    public ILogger CreateLogger(string categoryName) => new BarrierLogger(this, categoryName);

    public void Dispose() => release.Dispose();

    private sealed class BarrierLogger(SnapshotReadBarrier barrier, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (
                category != "Microsoft.EntityFrameworkCore.Database.Command"
                || eventId.Id != 20101
                || Volatile.Read(ref barrier.armed) == 0
            )
                return;
            string message = formatter(state, exception);
            if (
                !message.Contains(
                    "FROM inventory.stock_item_reference_feed",
                    StringComparison.Ordinal
                ) || message.Contains("FOR UPDATE", StringComparison.Ordinal)
            )
                return;
            if (Interlocked.Exchange(ref barrier.armed, 0) != 1)
                return;
            barrier.entered.TrySetResult();
            if (!barrier.release.Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("Test did not release the snapshot read barrier.");
        }
    }
}
