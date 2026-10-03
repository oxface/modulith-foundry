using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Access.Composition;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.ExtensionPoints;
using ModulithFoundry.Modules.Inventory.Composition;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Purchasing.Composition;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Sales.Composition;
using ModulithFoundry.Modules.Sales.Contracts;
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

internal sealed class SalesFulfilmentFixture : IAsyncDisposable
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6").Build();
    private readonly RabbitMqContainer rabbit = new RabbitMqBuilder("rabbitmq:4.3.6-management")
        .WithPortBinding(15672, true)
        .Build();
    private readonly CancellationTokenSource timeout =
        CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private IHost host = null!;
    private IHost? observer;
    private IHost? errorObserver;
    private IHost? replenishmentObserver;
    private readonly Channel<ReplenishmentRequirementCreatedV1> createdRequirements =
        Channel.CreateUnbounded<ReplenishmentRequirementCreatedV1>();
    private readonly Channel<ReplenishmentRequirementCreatedV1> replenishmentErrors =
        Channel.CreateUnbounded<ReplenishmentRequirementCreatedV1>();
    private readonly Channel<StockReservationOutcomeV1> errors =
        Channel.CreateUnbounded<StockReservationOutcomeV1>();
    private bool controlledOutcomes;
    private bool enablePurchasing;
    private ILoggerProvider? logProvider;
    private readonly Channel<ReserveStockV1> commands = Channel.CreateUnbounded<ReserveStockV1>();
    private readonly Channel<ReleaseReservationV1> releaseCommands =
        Channel.CreateUnbounded<ReleaseReservationV1>();
    private readonly Channel<StockReservationReleaseOutcomeV1> releaseErrors =
        Channel.CreateUnbounded<StockReservationReleaseOutcomeV1>();
    private readonly DeliveryProbe deliveries = new();
    internal OrganizationAccessContext Administrator { get; private set; } = null!;
    internal OrganizationAccessContext Approver { get; private set; } = null!;
    internal StockItemId Item { get; private set; }
    internal StockingLocationId Location { get; private set; }
    internal CancellationToken CancellationToken => timeout.Token;
    internal IMeterFactory MeterFactory => host.Services.GetRequiredService<IMeterFactory>();
    internal string DatabaseConnectionString => postgres.GetConnectionString();
    internal string BrokerConnectionString => rabbit.GetConnectionString();

    internal BrokerQueueProbe ObserveBroker() =>
        new(
            new Uri($"http://{rabbit.Hostname}:{rabbit.GetMappedPublicPort(15672)}/"),
            BrokerConnectionString
        );

    internal static async Task<SalesFulfilmentFixture> StartAsync(
        bool createMain = true,
        bool controlledOutcomes = false,
        bool enablePurchasing = false,
        ILoggerProvider? logs = null,
        TimeSpan? scenarioTimeout = null
    )
    {
        var fixture = new SalesFulfilmentFixture();
        fixture.controlledOutcomes = controlledOutcomes;
        fixture.enablePurchasing = enablePurchasing;
        fixture.logProvider = logs;
        fixture.timeout.CancelAfter(scenarioTimeout ?? TimeSpan.FromSeconds(120));
        try
        {
            await fixture.postgres.StartAsync(fixture.CancellationToken);
            await fixture.rabbit.StartAsync(fixture.CancellationToken);
            fixture.host = fixture.BuildHost();
            await fixture.host.Services.MigrateAccessAsync(fixture.CancellationToken);
            await fixture.host.Services.MigrateInventoryAsync(fixture.CancellationToken);
            await fixture.host.Services.MigrateSalesAsync(fixture.CancellationToken);
            await fixture.host.Services.MigratePurchasingAsync(fixture.CancellationToken);
            if (controlledOutcomes)
            {
                fixture.observer = fixture.BuildObserver();
                await fixture.observer.StartAsync(fixture.CancellationToken);
            }
            await fixture.host.StartAsync(fixture.CancellationToken);
            await fixture.SeedAsync();
            if (createMain)
                await fixture.AddMainAsync();
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    private IHost BuildHost()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:rabbitmq"] = rabbit.GetConnectionString(),
                    ["Invitations:PublicApplicationUrl"] = "https://example.test",
                    ["Email:Smtp:Host"] = "localhost",
                    ["Email:Smtp:Port"] = "1025",
                    ["Email:Smtp:Security"] = "None",
                    ["Email:Smtp:FromAddress"] = "no-reply@example.test",
                }
            )
            .Build();
        IHostBuilder builder = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Warning);
                if (logProvider is not null)
                    logging.AddProvider(logProvider);
            })
            .ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddSingleton(_ => NpgsqlDataSource.Create(postgres.GetConnectionString()));
                services.AddScoped<ActorContextAccessor>();
                services.AddScoped<IOrganizationContextAccessor>(provider =>
                    provider.GetRequiredService<ActorContextAccessor>()
                );
                services.AddAccessModule(configuration);
                services.AddSingleton<CapturedEmailTransport>();
                services.Replace(
                    ServiceDescriptor.Singleton<IEmailTransport>(provider =>
                        provider.GetRequiredService<CapturedEmailTransport>()
                    )
                );
                services.AddInventoryModule();
                services.AddSalesModule();
                services.AddPurchasingModule();
            })
            .AddSalesMessaging(
                configuration,
                options =>
                    options.Decorate<IPipeline>(context =>
                        new PipelineStepInjector(context.Get<IPipeline>()).OnReceive(
                            deliveries,
                            PipelineRelativePosition.Before,
                            typeof(DispatchIncomingMessageStep)
                        )
                    )
            );
        if (!controlledOutcomes)
            builder.AddInventoryMessaging(configuration);
        if (enablePurchasing)
            builder.AddPurchasingMessaging(configuration);
        return builder.Build();
    }

    // Only controlled-outcome tests replace the remote peer. Sales still uses its real relay,
    // RabbitMQ receiver, inbox and process handler; successful stock mutations use the real peer.
    private IHost BuildObserver() =>
        Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            .ConfigureServices(services =>
            {
                services.AddSingleton(commands);
                services.AddSingleton(releaseCommands);
                services.AddRebusHandler<ReservationCommandObserver>();
                services.AddRebusHandler<ReleaseCommandObserver>();
                services.AddRebus(configure =>
                    configure
                        .Transport(transport =>
                            transport.UseRabbitMq(
                                rabbit.GetConnectionString(),
                                InventoryMessaging.InputQueue
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
                                            false
                                        ),
                                        new JsonStringEnumConverter<StockReservationReleaseOutcome>(
                                            JsonNamingPolicy.KebabCaseLower,
                                            false
                                        ),
                                    },
                                }
                            );
                            serializer
                                .UseCustomMessageTypeNames()
                                .AddWithCustomName<ReserveStockV1>(ReserveStockV1.LogicalName)
                                .AddWithCustomName<ReleaseReservationV1>(
                                    ReleaseReservationV1.LogicalName
                                )
                                .AddWithCustomName<StockReservationReleaseOutcomeV1>(
                                    StockReservationReleaseOutcomeV1.LogicalName
                                )
                                .AddWithCustomName<StockReservationOutcomeV1>(
                                    StockReservationOutcomeV1.LogicalName
                                );
                        })
                );
            })
            .Build();

    internal Task<ReserveStockV1> ReadCommandAsync() =>
        commands.Reader.ReadAsync(CancellationToken).AsTask();

    internal Task<ReleaseReservationV1> ReadReleaseCommandAsync() =>
        releaseCommands.Reader.ReadAsync(CancellationToken).AsTask();

    internal Task PublishAsync(StockReservationReleaseOutcomeV1 outcome) =>
        observer!
            .Services.GetRequiredService<IBus>()
            .Advanced.Topics.Publish(
                StockReservationReleaseOutcomeV1.LogicalName,
                outcome,
                new Dictionary<string, string>
                {
                    [Headers.MessageId] = outcome.MessageId.ToString(),
                    [Headers.CorrelationId] = outcome.ProcessId.ToString(),
                    ["causation-id"] = outcome.CausationId.ToString(),
                    ["producer-module"] = "inventory",
                }
            );

    internal Task PublishAsync(StockReservationOutcomeV1 outcome) =>
        observer!
            .Services.GetRequiredService<IBus>()
            .Advanced.Topics.Publish(
                StockReservationOutcomeV1.LogicalName,
                outcome,
                new Dictionary<string, string>
                {
                    [Headers.MessageId] = outcome.MessageId.ToString("D"),
                    ["producer-module"] = "inventory",
                }
            );

    internal Task WaitDeliveryAsync(Guid messageId, int count = 1) =>
        deliveries.WaitAsync(messageId, count, CancellationToken);

    internal void FailSettlementOnce(Guid messageId) => deliveries.FailOnce = messageId;

    internal async Task<StockReservationOutcomeV1> ReadErrorAsync()
    {
        await StartErrorObserverAsync();
        return await errors.Reader.ReadAsync(CancellationToken);
    }

    internal async Task<ReplenishmentRequirementCreatedV1> ReadReplenishmentErrorAsync()
    {
        await StartErrorObserverAsync();
        return await replenishmentErrors.Reader.ReadAsync(CancellationToken);
    }

    internal async Task<StockReservationReleaseOutcomeV1> ReadReleaseErrorAsync()
    {
        await StartErrorObserverAsync();
        return await releaseErrors.Reader.ReadAsync(CancellationToken);
    }

    private async Task StartErrorObserverAsync()
    {
        if (errorObserver is null)
        {
            errorObserver = Host.CreateDefaultBuilder()
                .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
                .ConfigureServices(services =>
                {
                    services.AddSingleton(errors);
                    services.AddSingleton(replenishmentErrors);
                    services.AddSingleton(releaseErrors);
                    services.AddRebusHandler<ReservationOutcomeErrorObserver>();
                    services.AddRebusHandler<RequirementOutcomeErrorObserver>();
                    services.AddRebusHandler<ReleaseOutcomeErrorObserver>();
                    services.AddRebus(configure =>
                        configure
                            .Transport(transport =>
                                transport.UseRabbitMq(
                                    rabbit.GetConnectionString(),
                                    SalesMessaging.ErrorQueue
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
                                                false
                                            ),
                                            new JsonStringEnumConverter<StockReservationReleaseOutcome>(
                                                JsonNamingPolicy.KebabCaseLower,
                                                false
                                            ),
                                        },
                                    }
                                );
                                serializer
                                    .UseCustomMessageTypeNames()
                                    .AddWithCustomName<StockReservationOutcomeV1>(
                                        StockReservationOutcomeV1.LogicalName
                                    )
                                    .AddWithCustomName<StockReservationReleaseOutcomeV1>(
                                        StockReservationReleaseOutcomeV1.LogicalName
                                    )
                                    .AddWithCustomName<ReplenishmentRequirementCreatedV1>(
                                        ReplenishmentRequirementCreatedV1.LogicalName
                                    );
                            })
                    );
                })
                .Build();
            await errorObserver.StartAsync(CancellationToken);
        }
    }

    internal async Task StartReplenishmentObserverAsync()
    {
        replenishmentObserver = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            .ConfigureServices(services =>
            {
                services.AddSingleton(createdRequirements);
                services.AddRebusHandler<RequirementCreatedObserver>();
                services.AddRebus(configure =>
                    configure
                        .Transport(transport =>
                            transport.UseRabbitMq(
                                rabbit.GetConnectionString(),
                                "test.sales-replenishment-observer"
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
        await replenishmentObserver.StartAsync(CancellationToken);
        await replenishmentObserver
            .Services.GetRequiredService<IBus>()
            .Advanced.Topics.Subscribe(ReplenishmentRequirementCreatedV1.LogicalName);
    }

    internal ValueTask<ReplenishmentRequirementCreatedV1> ReadCreatedAsync() =>
        createdRequirements.Reader.ReadAsync(CancellationToken);

    internal Task PublishAsync(ReplenishmentRequirementCreatedV1 outcome) =>
        replenishmentObserver!
            .Services.GetRequiredService<IBus>()
            .Advanced.Topics.Publish(
                ReplenishmentRequirementCreatedV1.LogicalName,
                outcome,
                new Dictionary<string, string>
                {
                    [Headers.MessageId] = outcome.MessageId.ToString(),
                    [Headers.CorrelationId] = outcome.ProcessId.ToString(),
                    ["causation-id"] = outcome.CausationId.ToString(),
                    ["producer-module"] = "purchasing",
                }
            );

    internal Task StopAsync() => host.StopAsync(CancellationToken);

    internal Task StopBrokerAsync() => rabbit.StopAsync(CancellationToken);

    internal Task StartBrokerAsync() => rabbit.StartAsync(CancellationToken);

    internal Task StopDatabaseAsync() => postgres.StopAsync(CancellationToken);

    internal Task StartDatabaseAsync() => postgres.StartAsync(CancellationToken);

    internal Task<CancelSalesOrderResult> CancelAsync(
        SalesOrderView order,
        string reason = "Customer withdrew"
    ) =>
        RunAsync(
            Administrator,
            services =>
                services
                    .GetRequiredService<ISalesOrderCancellation>()
                    .CancelAsync(
                        new(
                            Administrator.UserId,
                            Administrator.OrganizationId,
                            order.OrderNumber,
                            order.Version,
                            reason
                        ),
                        CancellationToken
                    )
        );

    // Controlled peer setup; assertions about physical stock use the real Inventory endpoint instead.
    internal async Task<(
        SalesOrderView Order,
        ReleaseReservationV1 Release
    )> PrepareControlledCancellationAsync()
    {
        var order = await ApproveAsync(4m);
        var reserve = await ReadCommandAsync();
        var outcome = new StockReservationOutcomeV1(
            Guid.CreateVersion7(),
            reserve.MessageId,
            reserve.OrganizationId,
            reserve.OperationId,
            reserve.ProcessId,
            reserve.OrderNumber,
            reserve.LineNumber,
            StockReservationOutcome.Reserved,
            Guid.CreateVersion7(),
            reserve.Quantity,
            6m,
            reserve.BaseUnitCode,
            null,
            DateTimeOffset.UtcNow
        );
        await PublishAsync(outcome);
        await WaitDeliveryAsync(outcome.MessageId);
        Assert.IsType<CancelSalesOrderResult.Cancelled>(await CancelAsync(order));
        return (order, await ReadReleaseCommandAsync());
    }

    internal async Task RestartAsync(bool addMain = false, bool? enablePurchasing = null)
    {
        await host.StopAsync(CancellationToken);
        host.Dispose();
        if (enablePurchasing.HasValue)
            this.enablePurchasing = enablePurchasing.Value;
        host = BuildHost();
        if (addMain)
            await AddMainAsync();
        await host.StartAsync(CancellationToken);
    }

    internal async Task ExecuteSetupAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync(CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken);
    }

    internal async Task<NpgsqlConnection> HoldProcessUpdatesAsync()
    {
        var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
            {
                Pooling = false,
            }.ConnectionString
        );
        await connection.OpenAsync(CancellationToken);
        await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(4571)", connection);
        await command.ExecuteNonQueryAsync(CancellationToken);
        await ExecuteSetupAsync(
            """
            CREATE FUNCTION sales.hold_process() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN PERFORM pg_advisory_xact_lock(4571); RETURN NEW; END $$;
            CREATE TRIGGER hold_process BEFORE UPDATE ON sales.fulfilment_processes
            FOR EACH ROW EXECUTE FUNCTION sales.hold_process();
            """
        );
        return connection;
    }

    internal async Task WaitForCompetingUpdatesAsync()
    {
        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync(CancellationToken);
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*) FROM pg_stat_activity WHERE wait_event_type = 'Lock'
                AND query ILIKE '%fulfilment_processes%' AND query ILIKE '%UPDATE%'
            """,
            connection
        );
        while ((long)(await command.ExecuteScalarAsync(CancellationToken))! < 2)
            await Task.Delay(25, CancellationToken);
    }

    private async Task SeedAsync()
    {
        Administrator = await RunAsync(
            null,
            async services =>
            {
                UserIdentityLink user = await services
                    .GetRequiredService<IExternalIdentityLinking>()
                    .LinkAsync(
                        ExternalIdentity.Create(
                            "https://issuer.example",
                            "administrator",
                            "administrator@example.test",
                            "Administrator"
                        ),
                        CancellationToken
                    );
                Assert.IsType<CreateOrganizationResult.Created>(
                    await services
                        .GetRequiredService<IOrganizationCreation>()
                        .CreateOrganizationAsync(
                            new(user.UserId, "Fulfilment", "fulfilment"),
                            CancellationToken
                        )
                );
                var queries = services.GetRequiredService<IOrganizationQueries>();
                OrganizationAccessContext actor = (
                    await queries.ResolveAccessAsync(user.UserId, "fulfilment", CancellationToken)
                )!;
                services.GetRequiredService<ActorContextAccessor>().OrganizationContext = actor;
                Assert.IsType<ReplaceMembershipRolesResult.Updated>(
                    await services
                        .GetRequiredService<IOrganizationMembershipAdministration>()
                        .ReplaceRolesAsync(
                            new(
                                actor.UserId,
                                actor.OrganizationId,
                                actor.MembershipId,
                                [
                                    SystemRoleIds.OrganizationAdministrator,
                                    SalesRoleIds.Manager,
                                    SalesRoleIds.Clerk,
                                    InventoryRoleIds.Manager,
                                    PurchasingRoleIds.Agent,
                                ]
                            ),
                            CancellationToken
                        )
                );
                return (
                    await queries.ResolveAccessAsync(user.UserId, "fulfilment", CancellationToken)
                )!;
            }
        );
        Approver = await RunAsync(
            Administrator,
            async services =>
            {
                var invitations = services.GetRequiredService<IOrganizationInvitationOperations>();
                InvitationId invitation = Assert
                    .IsType<CreateOrganizationInvitationResult.Created>(
                        await invitations.CreateInvitationAsync(
                            new(
                                Administrator.UserId,
                                Administrator.OrganizationId,
                                "approver@example.test",
                                [SalesRoleIds.Approver]
                            ),
                            CancellationToken
                        )
                    )
                    .Invitation.InvitationId;
                EmailMessage email = await host
                    .Services.GetRequiredService<CapturedEmailTransport>()
                    .ReadAsync(CancellationToken);
                const string prefix = "Accept the invitation: ";
                var uri = new Uri(
                    email
                        .TextBody.Split('\n', StringSplitOptions.TrimEntries)
                        .Single(line => line.StartsWith(prefix, StringComparison.Ordinal))[
                        prefix.Length..
                    ]
                );
                string secret = uri
                    .Query.TrimStart('?')
                    .Split('&')
                    .Select(part => part.Split('=', 2))
                    .Where(part => part[0] == "code")
                    .Select(part => Uri.UnescapeDataString(part[1]))
                    .Single();
                UserIdentityLink user = await services
                    .GetRequiredService<IExternalIdentityLinking>()
                    .LinkAsync(
                        ExternalIdentity.Create(
                            "https://issuer.example",
                            "approver",
                            "approver@example.test",
                            "Approver"
                        ),
                        CancellationToken
                    );
                Assert.IsType<AcceptOrganizationInvitationResult.Accepted>(
                    await invitations.AcceptInvitationAsync(
                        new(user.UserId, invitation, secret, "approver@example.test"),
                        CancellationToken
                    )
                );
                return (
                    await services
                        .GetRequiredService<IOrganizationQueries>()
                        .ResolveAccessAsync(user.UserId, "fulfilment", CancellationToken)
                )!;
            }
        );
        await RunAsync(
            Administrator,
            async services =>
            {
                Assert.IsType<CreateCustomerResult.Created>(
                    await services
                        .GetRequiredService<ICustomerAdministration>()
                        .CreateAsync(
                            new(
                                Administrator.UserId,
                                Administrator.OrganizationId,
                                "BUYER",
                                "Buyer"
                            ),
                            CancellationToken
                        )
                );
                Item = Assert
                    .IsType<CreateStockItemResult.Created>(
                        await services
                            .GetRequiredService<IStockItemAdministration>()
                            .CreateAsync(
                                new(
                                    Administrator.UserId,
                                    Administrator.OrganizationId,
                                    "BOLT",
                                    "Bolt",
                                    "EA"
                                ),
                                CancellationToken
                            )
                    )
                    .Item.StockItemId;
                Assert.IsType<SetSalesApprovalAuthorityResult.Saved>(
                    await services
                        .GetRequiredService<ISalesApprovalAuthorityAdministration>()
                        .SetAsync(
                            new(
                                Administrator.UserId,
                                Administrator.OrganizationId,
                                Approver.MembershipId,
                                1000m,
                                "USD",
                                0
                            ),
                            CancellationToken
                        )
                );
            }
        );
    }

    internal Task AddMainAsync() =>
        RunAsync(
            Administrator,
            async services =>
            {
                Location = Assert
                    .IsType<CreateStockingLocationResult.Created>(
                        await services
                            .GetRequiredService<IStockingLocationAdministration>()
                            .CreateAsync(
                                new(
                                    Administrator.UserId,
                                    Administrator.OrganizationId,
                                    "MAIN",
                                    "Main"
                                ),
                                CancellationToken
                            )
                    )
                    .Location.StockingLocationId;
                Assert.IsType<RecordStockReceiptResult.Recorded>(
                    await services
                        .GetRequiredService<IStockPositionOperations>()
                        .RecordReceiptAsync(
                            new(
                                Administrator.UserId,
                                Administrator.OrganizationId,
                                "MAIN",
                                "BOLT",
                                10m,
                                0
                            ),
                            CancellationToken
                        )
                );
            }
        );

    internal async Task<SalesOrderView> ApproveAsync(params decimal[] quantities)
    {
        SalesOrderView order = await SubmitAsync(quantities);
        return await ApproveAsync(order);
    }

    internal Task<SalesOrderView> SubmitAsync(params decimal[] quantities) =>
        RunAsync(
            Administrator,
            async services =>
            {
                var operations = services.GetRequiredService<ISalesOrderOperations>();
                SalesOrderView draft = Assert
                    .IsType<CreateDraftSalesOrderResult.Created>(
                        await operations.CreateDraftAsync(
                            new(
                                Administrator.UserId,
                                Administrator.OrganizationId,
                                "BUYER",
                                "USD",
                                [
                                    .. quantities.Select(quantity => new DraftSalesOrderLine(
                                        Item,
                                        quantity,
                                        1m
                                    )),
                                ]
                            ),
                            CancellationToken
                        )
                    )
                    .Order;
                return Assert
                    .IsType<SubmitSalesOrderResult.Submitted>(
                        await operations.SubmitAsync(
                            new(
                                Administrator.UserId,
                                Administrator.OrganizationId,
                                draft.OrderNumber,
                                draft.Version
                            ),
                            CancellationToken
                        )
                    )
                    .Order;
            }
        );

    internal Task<SalesOrderView> ApproveAsync(SalesOrderView order) =>
        RunAsync(
            Approver,
            async services =>
                Assert
                    .IsType<ApproveSalesOrderResult.Approved>(
                        await services
                            .GetRequiredService<ISalesOrderApproval>()
                            .ApproveAsync(
                                new(
                                    Approver.UserId,
                                    Approver.OrganizationId,
                                    order.OrderNumber,
                                    order.Version
                                ),
                                CancellationToken
                            )
                    )
                    .Order
        );

    internal Task<SalesOrderView> ReadOrderAsync(long number) =>
        RunAsync(
            Administrator,
            async services =>
                Assert
                    .IsType<GetSalesOrderResult.Found>(
                        await services
                            .GetRequiredService<ISalesOrderOperations>()
                            .GetByNumberAsync(
                                Administrator.UserId,
                                Administrator.OrganizationId,
                                number,
                                CancellationToken
                            )
                    )
                    .Order
        );

    internal Task<IReadOnlyList<SalesOrderActivityEntry>> ActivityAsync(long number) =>
        RunAsync(
            Administrator,
            async services =>
                Assert
                    .IsType<GetSalesOrderActivityResult.Found>(
                        await services
                            .GetRequiredService<ISalesOrderOperations>()
                            .GetActivityAsync(
                                Administrator.UserId,
                                Administrator.OrganizationId,
                                number,
                                CancellationToken
                            )
                    )
                    .Entries
        );

    internal Task<OrderFulfilmentView> ReadAsync(long orderNumber) =>
        RunAsync(
            Administrator,
            async services =>
                Assert
                    .IsType<GetOrderFulfilmentResult.Found>(
                        await services
                            .GetRequiredService<ISalesOrderOperations>()
                            .GetFulfilmentAsync(
                                Administrator.UserId,
                                Administrator.OrganizationId,
                                orderNumber,
                                CancellationToken
                            )
                    )
                    .Process
        );

    internal async Task<OrderFulfilmentView> WaitAsync(
        long orderNumber,
        string status,
        TimeSpan? completionTimeout = null
    )
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        bound.CancelAfter(completionTimeout ?? TimeSpan.FromSeconds(30));
        while (true)
        {
            OrderFulfilmentView view = await ReadAsync(orderNumber);
            if (OrderFulfilmentStatusValues.ToValue(view.Status) == status)
                return view;
            await Task.Delay(TimeSpan.FromMilliseconds(50), bound.Token);
        }
    }

    internal Task<StockPositionView> StockAsync() =>
        RunAsync(
            Administrator,
            async services =>
                Assert
                    .IsType<GetStockPositionResult.Found>(
                        await services
                            .GetRequiredService<IStockPositionOperations>()
                            .GetCurrentAsync(
                                new(
                                    Administrator.UserId,
                                    Administrator.OrganizationId,
                                    "MAIN",
                                    "BOLT"
                                ),
                                CancellationToken
                            )
                    )
                    .Position
        );

    internal Task<GetReplenishmentRequirementResult> RequirementAsync(long number) =>
        RunAsync(
            Administrator,
            services =>
                services
                    .GetRequiredService<IReplenishmentRequirementQueries>()
                    .GetByNumberAsync(
                        Administrator.UserId,
                        Administrator.OrganizationId,
                        number,
                        CancellationToken
                    )
        );

    internal async Task<IReadOnlyList<ReplenishmentRequirementView>> RequirementsAsync() =>
        Assert
            .IsType<ListReplenishmentRequirementsResult.Listed>(
                await RunAsync(
                    Administrator,
                    services =>
                        services
                            .GetRequiredService<IReplenishmentRequirementQueries>()
                            .ListAsync(
                                Administrator.UserId,
                                Administrator.OrganizationId,
                                cancellationToken: CancellationToken
                            )
                )
            )
            .Requirements;

    internal async Task<OrderFulfilmentView> WaitForRequirementAsync(
        long orderNumber,
        bool rejected = false
    )
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            var process = await ReadAsync(orderNumber);
            if (
                process.Lines.Any(line =>
                    rejected
                        ? line.ReplenishmentReasonCode != null
                        : line.ReplenishmentRequirementId.HasValue
                )
            )
                return process;
            await Task.Delay(50, bound.Token);
        }
    }

    internal async Task DeactivateItemAsync()
    {
        await RunAsync(
            Administrator,
            async services =>
                Assert.IsType<SetStockItemActiveResult.Changed>(
                    await services
                        .GetRequiredService<IStockItemAdministration>()
                        .SetActiveAsync(
                            new(Administrator.UserId, Administrator.OrganizationId, "BOLT", false),
                            CancellationToken
                        )
                )
        );
        // Controlled reservation peers do not run Inventory's relay; use the real snapshot repair
        // Contract to install the changed reference before publishing their controlled outcome.
        await RunAsync(
            Administrator,
            services =>
                services
                    .GetRequiredService<IStockItemProjectionReconciliation>()
                    .RepairAsync(CancellationToken)
        );
        while (true)
        {
            var reference = await RunAsync(
                Administrator,
                services =>
                    services
                        .GetRequiredService<IStockItemProjectionQueries>()
                        .GetAsync(Administrator.OrganizationId, Item.Value, CancellationToken)
            );
            if (reference is { IsActive: false })
                return;
            await Task.Delay(50, CancellationToken);
        }
    }

    internal async Task<T> RunAsync<T>(
        OrganizationAccessContext? actor,
        Func<IServiceProvider, Task<T>> operation
    )
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ActorContextAccessor>().OrganizationContext =
            actor;
        return await operation(scope.ServiceProvider);
    }

    internal async Task RunAsync(
        OrganizationAccessContext? actor,
        Func<IServiceProvider, Task> operation
    )
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ActorContextAccessor>().OrganizationContext =
            actor;
        await operation(scope.ServiceProvider);
    }

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
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
        if (replenishmentObserver is not null)
        {
            await replenishmentObserver.StopAsync(CancellationToken.None);
            replenishmentObserver.Dispose();
        }
        await rabbit.DisposeAsync();
        await postgres.DisposeAsync();
        timeout.Dispose();
    }

    private sealed class ActorContextAccessor : IOrganizationContextAccessor
    {
        public OrganizationAccessContext? OrganizationContext { get; set; }
    }

    private sealed class CapturedEmailTransport : IEmailTransport
    {
        private readonly Channel<EmailMessage> messages = Channel.CreateUnbounded<EmailMessage>();

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            messages.Writer.TryWrite(message);
            return Task.CompletedTask;
        }

        internal Task<EmailMessage> ReadAsync(CancellationToken cancellationToken) =>
            messages.Reader.ReadAsync(cancellationToken).AsTask();
    }
}

internal sealed class ReservationCommandObserver(Channel<ReserveStockV1> commands)
    : IHandleMessages<ReserveStockV1>
{
    public Task Handle(ReserveStockV1 message) => commands.Writer.WriteAsync(message).AsTask();
}

internal sealed class ReleaseCommandObserver(Channel<ReleaseReservationV1> commands)
    : IHandleMessages<ReleaseReservationV1>
{
    public Task Handle(ReleaseReservationV1 message) =>
        commands.Writer.WriteAsync(message).AsTask();
}

internal sealed class ReleaseOutcomeErrorObserver(Channel<StockReservationReleaseOutcomeV1> errors)
    : IHandleMessages<StockReservationReleaseOutcomeV1>
{
    public Task Handle(StockReservationReleaseOutcomeV1 message) =>
        errors.Writer.WriteAsync(message).AsTask();
}

internal sealed class ReservationOutcomeErrorObserver(Channel<StockReservationOutcomeV1> errors)
    : IHandleMessages<StockReservationOutcomeV1>
{
    public Task Handle(StockReservationOutcomeV1 message) =>
        errors.Writer.WriteAsync(message).AsTask();
}

internal sealed class RequirementOutcomeErrorObserver(
    Channel<ReplenishmentRequirementCreatedV1> errors
) : IHandleMessages<ReplenishmentRequirementCreatedV1>
{
    public Task Handle(ReplenishmentRequirementCreatedV1 message) =>
        errors.Writer.WriteAsync(message).AsTask();
}
