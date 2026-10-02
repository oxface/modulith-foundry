using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Composition;
using ModulithFoundry.Modules.Inventory.Contracts;
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

internal sealed class ReservationFixture : IAsyncDisposable
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private readonly RabbitMqContainer rabbit = new RabbitMqBuilder("rabbitmq:4.3.6-management")
        .WithPortBinding(15672, true)
        .Build();
    private readonly Channel<ObservedOutcome> outcomes = Channel.CreateUnbounded<ObservedOutcome>();
    private readonly Channel<StockReservationReleaseOutcomeV1> releaseOutcomes =
        Channel.CreateUnbounded<StockReservationReleaseOutcomeV1>();
    private readonly DeliveryProbe deliveries = new();
    private readonly CancellationTokenSource timeout =
        CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private IHost receiver = null!;
    private IHost observer = null!;
    private IHost? errorObserver;
    private readonly Channel<ReserveStockV1> errors = Channel.CreateUnbounded<ReserveStockV1>();
    private readonly Channel<ReleaseReservationV1> releaseErrors =
        Channel.CreateUnbounded<ReleaseReservationV1>();
    private NpgsqlDataSource dataSource = null!;
    private ILoggerProvider? logProvider;
    private StockItemId itemId;
    private StockingLocationId locationId;

    internal OrganizationAccessContext Actor { get; } =
        new(
            new UserId(Guid.CreateVersion7()),
            new OrganizationId(Guid.CreateVersion7()),
            new MembershipId(Guid.CreateVersion7()),
            "Broker Test",
            "broker-test",
            []
        );

    internal CancellationToken CancellationToken => timeout.Token;
    internal IMeterFactory MeterFactory => receiver.Services.GetRequiredService<IMeterFactory>();

    internal string DatabaseConnectionString => postgres.GetConnectionString();

    internal string BrokerConnectionString => rabbit.GetConnectionString();

    internal BrokerQueueProbe ObserveBroker() =>
        new(
            new Uri($"http://{rabbit.Hostname}:{rabbit.GetMappedPublicPort(15672)}/"),
            BrokerConnectionString
        );

    internal static async Task<ReservationFixture> StartAsync(
        bool startReceiver = true,
        ILoggerProvider? logs = null
    )
    {
        var fixture = new ReservationFixture();
        fixture.logProvider = logs;
        fixture.timeout.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            await fixture.postgres.StartAsync(fixture.timeout.Token);
            await fixture.rabbit.StartAsync(fixture.timeout.Token);
            fixture.dataSource = NpgsqlDataSource.Create(fixture.postgres.GetConnectionString());
            fixture.receiver = fixture.BuildReceiver();
            await fixture.receiver.Services.MigrateInventoryAsync(fixture.timeout.Token);
            await using (AsyncServiceScope scope = fixture.receiver.Services.CreateAsyncScope())
            {
                IServiceProvider services = scope.ServiceProvider;
                fixture.itemId = Assert
                    .IsType<CreateStockItemResult.Created>(
                        await services
                            .GetRequiredService<IStockItemAdministration>()
                            .CreateAsync(
                                new(
                                    fixture.Actor.UserId,
                                    fixture.Actor.OrganizationId,
                                    "BOLT",
                                    "Bolt",
                                    "EA"
                                ),
                                fixture.timeout.Token
                            )
                    )
                    .Item.StockItemId;
                fixture.locationId = Assert
                    .IsType<CreateStockingLocationResult.Created>(
                        await services
                            .GetRequiredService<IStockingLocationAdministration>()
                            .CreateAsync(
                                new(
                                    fixture.Actor.UserId,
                                    fixture.Actor.OrganizationId,
                                    "MAIN",
                                    "Main"
                                ),
                                fixture.timeout.Token
                            )
                    )
                    .Location.StockingLocationId;
                Assert.IsType<RecordStockReceiptResult.Recorded>(
                    await services
                        .GetRequiredService<IStockPositionOperations>()
                        .RecordReceiptAsync(
                            new(
                                fixture.Actor.UserId,
                                fixture.Actor.OrganizationId,
                                "MAIN",
                                "BOLT",
                                10m,
                                0
                            ),
                            fixture.timeout.Token
                        )
                );
            }
            fixture.observer = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(fixture.outcomes);
                    services.AddSingleton(fixture.releaseOutcomes);
                    services.AddRebusHandler<OutcomeObserver>();
                    services.AddRebusHandler<ReleaseOutcomeObserver>();
                    services.AddRebus(
                        configure =>
                            configure
                                .Transport(transport =>
                                    transport.UseRabbitMq(
                                        fixture.rabbit.GetConnectionString(),
                                        "test.observer"
                                    )
                                )
                                .Serialization(serializer =>
                                {
                                    serializer.UseSystemTextJson(
                                        new JsonSerializerOptions(JsonSerializerDefaults.Web)
                                        {
                                            RespectNullableAnnotations = true,
                                            Converters =
                                            {
                                                new JsonStringEnumConverter<StockReservationOutcome>(
                                                    JsonNamingPolicy.KebabCaseLower,
                                                    allowIntegerValues: false
                                                ),
                                                new JsonStringEnumConverter<StockReservationReleaseOutcome>(
                                                    JsonNamingPolicy.KebabCaseLower,
                                                    allowIntegerValues: false
                                                ),
                                            },
                                        }
                                    );
                                    serializer
                                        .UseCustomMessageTypeNames()
                                        .AddWithCustomName<ReserveStockV1>(
                                            ReserveStockV1.LogicalName
                                        )
                                        .AddWithCustomName<StockReservationOutcomeV1>(
                                            StockReservationOutcomeV1.LogicalName
                                        )
                                        .AddWithCustomName<ReleaseReservationV1>(
                                            ReleaseReservationV1.LogicalName
                                        )
                                        .AddWithCustomName<StockReservationReleaseOutcomeV1>(
                                            StockReservationReleaseOutcomeV1.LogicalName
                                        );
                                }),
                        onCreated: async bus =>
                        {
                            await bus.Advanced.Topics.Subscribe(
                                StockReservationOutcomeV1.LogicalName
                            );
                            await bus.Advanced.Topics.Subscribe(
                                StockReservationReleaseOutcomeV1.LogicalName
                            );
                        }
                    );
                })
                .Build();
            await fixture.observer.StartAsync(fixture.timeout.Token);
            if (startReceiver)
                await fixture.receiver.StartAsync(fixture.timeout.Token);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    internal ReserveStockV1 Command(decimal quantity) =>
        new(
            Guid.CreateVersion7(),
            Actor.OrganizationId.Value,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            1,
            1,
            itemId.Value,
            locationId.Value,
            quantity,
            "EA",
            DateTimeOffset.UtcNow
        );

    internal Task SendAsync(
        ReserveStockV1 command,
        string producer = "sales",
        Guid? transportMessageId = null
    ) =>
        observer
            .Services.GetRequiredService<IBus>()
            .Advanced.Routing.Send(
                InventoryMessaging.InputQueue,
                command,
                new Dictionary<string, string>
                {
                    [Headers.MessageId] = (transportMessageId ?? command.MessageId).ToString(),
                    [Headers.CorrelationId] = command.ProcessId.ToString(),
                    ["producer-module"] = producer,
                }
            );

    internal static ReleaseReservationV1 ReleaseCommand(
        ReserveStockV1 original,
        StockReservationOutcomeV1 reserved
    ) =>
        new(
            Guid.CreateVersion7(),
            original.OrganizationId,
            Guid.CreateVersion7(),
            original.ProcessId,
            original.OrderNumber,
            original.LineNumber,
            original.OperationId,
            reserved.ReservationId!.Value,
            original.StockItemId,
            original.StockingLocationId,
            DateTimeOffset.UtcNow
        );

    internal Task SendAsync(
        ReleaseReservationV1 command,
        string producer = "sales",
        Guid? transportMessageId = null,
        Guid? correlationId = null
    ) =>
        observer
            .Services.GetRequiredService<IBus>()
            .Advanced.Routing.Send(
                InventoryMessaging.InputQueue,
                command,
                new Dictionary<string, string>
                {
                    [Headers.MessageId] = (transportMessageId ?? command.MessageId).ToString(),
                    [Headers.CorrelationId] = (correlationId ?? command.ProcessId).ToString(),
                    ["producer-module"] = producer,
                }
            );

    internal ValueTask<StockReservationReleaseOutcomeV1> ReadReleaseOutcomeAsync() =>
        releaseOutcomes.Reader.ReadAsync(timeout.Token);

    internal bool TryReadReleaseOutcome(out StockReservationReleaseOutcomeV1? outcome) =>
        releaseOutcomes.Reader.TryRead(out outcome);

    internal async ValueTask<StockReservationOutcomeV1> ReadOutcomeAsync()
    {
        ObservedOutcome observed = await outcomes.Reader.ReadAsync(timeout.Token);
        Assert.Equal(observed.Message.MessageId.ToString(), observed.TransportMessageId);
        return observed.Message;
    }

    internal bool TryReadOutcome(out StockReservationOutcomeV1? outcome)
    {
        bool found = outcomes.Reader.TryRead(out ObservedOutcome? observed);
        outcome = observed?.Message;
        return found;
    }

    internal Task WaitForProcessedAsync(Guid messageId, int count = 1) =>
        deliveries.WaitAsync(messageId, count, timeout.Token);

    internal void FailSettlementOnce(Guid messageId) => deliveries.FailOnce = messageId;

    private IHost BuildReceiver()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:rabbitmq"] = rabbit.GetConnectionString(),
                }
            )
            .Build();
        return Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Warning);
                if (logProvider is not null)
                    logging.AddProvider(logProvider);
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton(dataSource);
                services.AddSingleton<IOrganizationContextAccessor>(new TestActor(Actor));
                services.AddSingleton<IOrganizationAuthorization, AllowedAuthorization>();
                services.AddInventoryModule();
            })
            .AddInventoryMessaging(
                configuration,
                options =>
                    options.Decorate<IPipeline>(context =>
                        new PipelineStepInjector(context.Get<IPipeline>()).OnReceive(
                            deliveries,
                            PipelineRelativePosition.Before,
                            typeof(DispatchIncomingMessageStep)
                        )
                    )
            )
            .Build();
    }

    internal async Task RestartReceiverAsync()
    {
        await receiver.StopAsync(timeout.Token);
        receiver.Dispose();
        receiver = BuildReceiver();
        await receiver.StartAsync(timeout.Token);
    }

    internal Task StopReceiverAsync() => receiver.StopAsync(timeout.Token);

    internal async Task DeactivateReferencesAsync()
    {
        await using AsyncServiceScope scope = receiver.Services.CreateAsyncScope();
        Assert.IsType<SetStockItemActiveResult.Changed>(
            await scope
                .ServiceProvider.GetRequiredService<IStockItemAdministration>()
                .SetActiveAsync(
                    new(Actor.UserId, Actor.OrganizationId, "BOLT", false),
                    timeout.Token
                )
        );
        Assert.IsType<SetStockingLocationActiveResult.Changed>(
            await scope
                .ServiceProvider.GetRequiredService<IStockingLocationAdministration>()
                .SetActiveAsync(
                    new(Actor.UserId, Actor.OrganizationId, "MAIN", false),
                    timeout.Token
                )
        );
    }

    internal async Task ExecuteSqlAsync(string sql)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(
            timeout.Token
        );
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(timeout.Token);
    }

    internal async Task StartErrorObserverAsync()
    {
        errorObserver = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(errors);
                services.AddSingleton(releaseErrors);
                services.AddRebusHandler<ErrorObserver>();
                services.AddRebusHandler<ReleaseErrorObserver>();
                services.AddRebus(configure =>
                    configure
                        .Transport(transport =>
                            transport.UseRabbitMq(
                                rabbit.GetConnectionString(),
                                InventoryMessaging.ErrorQueue
                            )
                        )
                        .Serialization(serializer =>
                        {
                            serializer.UseSystemTextJson(
                                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                            );
                            serializer
                                .UseCustomMessageTypeNames()
                                .AddWithCustomName<ReserveStockV1>(ReserveStockV1.LogicalName)
                                .AddWithCustomName<ReleaseReservationV1>(
                                    ReleaseReservationV1.LogicalName
                                );
                        })
                );
            })
            .Build();
        await errorObserver.StartAsync(timeout.Token);
    }

    internal ValueTask<ReserveStockV1> ReadErrorAsync() => errors.Reader.ReadAsync(timeout.Token);

    internal ValueTask<ReleaseReservationV1> ReadReleaseErrorAsync() =>
        releaseErrors.Reader.ReadAsync(timeout.Token);

    internal async Task<StockPositionView> StockAsync()
    {
        await using AsyncServiceScope scope = receiver.Services.CreateAsyncScope();
        return Assert
            .IsType<GetStockPositionResult.Found>(
                await scope
                    .ServiceProvider.GetRequiredService<IStockPositionOperations>()
                    .GetCurrentAsync(
                        new(Actor.UserId, Actor.OrganizationId, "MAIN", "BOLT"),
                        timeout.Token
                    )
            )
            .Position;
    }

    internal async Task RebuildAsync(StockPositionId id, bool previousModelMatched = true)
    {
        await using AsyncServiceScope scope = receiver.Services.CreateAsyncScope();
        StockPositionRebuildResult result = await scope
            .ServiceProvider.GetRequiredService<IStockPositionProjectionRebuilder>()
            .RebuildAsync(new(Actor.UserId, Actor.OrganizationId, id), timeout.Token);
        Assert.Equal(
            previousModelMatched,
            Assert.IsType<StockPositionRebuildResult.Rebuilt>(result).PreviousModelMatched
        );
    }

    internal async Task<StockPositionView> StockAtVersionAsync(long version)
    {
        await using AsyncServiceScope scope = receiver.Services.CreateAsyncScope();
        return Assert
            .IsType<GetStockPositionResult.Found>(
                await scope
                    .ServiceProvider.GetRequiredService<IStockPositionOperations>()
                    .GetAtVersionAsync(
                        new(Actor.UserId, Actor.OrganizationId, "MAIN", "BOLT", version),
                        timeout.Token
                    )
            )
            .Position;
    }

    internal async Task<StockPositionHistoryView> HistoryAsync()
    {
        await using AsyncServiceScope scope = receiver.Services.CreateAsyncScope();
        return Assert
            .IsType<GetStockPositionHistoryResult.Found>(
                await scope
                    .ServiceProvider.GetRequiredService<IStockPositionOperations>()
                    .GetHistoryAsync(
                        new(Actor.UserId, Actor.OrganizationId, "MAIN", "BOLT"),
                        timeout.Token
                    )
            )
            .History;
    }

    public async ValueTask DisposeAsync()
    {
        if (receiver is not null)
        {
            await receiver.StopAsync(CancellationToken.None);
            receiver.Dispose();
        }
        if (observer is not null)
        {
            await observer.StopAsync(CancellationToken.None);
            observer.Dispose();
        }
        if (errorObserver is not null)
        {
            await errorObserver.StopAsync(CancellationToken.None);
            errorObserver.Dispose();
        }
        if (dataSource is not null)
            await dataSource.DisposeAsync();
        await rabbit.DisposeAsync();
        await postgres.DisposeAsync();
        timeout.Dispose();
    }

    private sealed class TestActor(OrganizationAccessContext context) : IOrganizationContextAccessor
    {
        public OrganizationAccessContext? OrganizationContext => context;
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

// Test-only delivery-window probe. No production handler or transaction is replaced.
internal sealed class DeliveryProbe : IIncomingStep
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, int> counts = new();
    private readonly Channel<Guid> completed = Channel.CreateUnbounded<Guid>();
    internal Guid? FailOnce { get; set; }
    private int injected;

    public async Task Process(IncomingStepContext context, Func<Task> next)
    {
        await next();
        Guid id = Guid.Parse(context.Load<TransportMessage>().Headers[Headers.MessageId]);
        counts.AddOrUpdate(id, 1, (_, current) => current + 1);
        await completed.Writer.WriteAsync(id);
        if (FailOnce == id && Interlocked.Exchange(ref injected, 1) == 0)
            throw new IOException("Test fault after database commit and before broker settlement.");
    }

    internal async Task WaitAsync(Guid id, int count, CancellationToken cancellationToken)
    {
        while (counts.GetValueOrDefault(id) < count)
            await completed.Reader.ReadAsync(cancellationToken);
    }
}

internal sealed record ObservedOutcome(
    StockReservationOutcomeV1 Message,
    string TransportMessageId
);

internal sealed class OutcomeObserver(Channel<ObservedOutcome> outcomes)
    : IHandleMessages<StockReservationOutcomeV1>
{
    public Task Handle(StockReservationOutcomeV1 message) =>
        outcomes
            .Writer.WriteAsync(new(message, MessageContext.Current.Headers[Headers.MessageId]))
            .AsTask();
}

internal sealed class ErrorObserver(Channel<ReserveStockV1> errors)
    : IHandleMessages<ReserveStockV1>
{
    public Task Handle(ReserveStockV1 message) => errors.Writer.WriteAsync(message).AsTask();
}

internal sealed class ReleaseOutcomeObserver(Channel<StockReservationReleaseOutcomeV1> outcomes)
    : IHandleMessages<StockReservationReleaseOutcomeV1>
{
    public Task Handle(StockReservationReleaseOutcomeV1 message)
    {
        var headers = MessageContext.Current.Headers;
        Assert.Equal(message.MessageId.ToString(), headers[Headers.MessageId]);
        Assert.Equal(message.ProcessId.ToString(), headers[Headers.CorrelationId]);
        Assert.Equal(message.CausationId.ToString(), headers["causation-id"]);
        Assert.Equal("inventory", headers["producer-module"]);
        return outcomes.Writer.WriteAsync(message).AsTask();
    }
}

internal sealed class ReleaseErrorObserver(Channel<ReleaseReservationV1> errors)
    : IHandleMessages<ReleaseReservationV1>
{
    public Task Handle(ReleaseReservationV1 message) => errors.Writer.WriteAsync(message).AsTask();
}
