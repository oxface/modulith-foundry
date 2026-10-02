using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Sales.Fulfilment.QueuePendingFulfilment;
using ModulithFoundry.Modules.Sales.Fulfilment.RecordReleaseOutcome;
using ModulithFoundry.Modules.Sales.Fulfilment.RecordReplenishmentOutcome;
using ModulithFoundry.Modules.Sales.Fulfilment.RecordReservationOutcome;
using ModulithFoundry.Modules.Sales.Messaging;
using Npgsql;
using Rebus.Config;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Serialization.Custom;
using Rebus.Serialization.Json;

namespace ModulithFoundry.Modules.Sales.Composition;

public static class SalesMessaging
{
    public const string InputQueue = "modulith-foundry.sales";
    public const string ErrorQueue = "modulith-foundry.sales.error";

    public static IHostBuilder AddSalesMessaging(
        this IHostBuilder host,
        IConfiguration configuration,
        Action<OptionsConfigurer>? configureOptions = null
    )
    {
        string connection =
            configuration.GetConnectionString("rabbitmq")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:rabbitmq is required for Sales messaging."
            );
        string inventoryQueue =
            configuration["Messaging:InventoryQueue"] ?? "modulith-foundry.inventory";
        string purchasingQueue =
            configuration["Messaging:PurchasingQueue"] ?? "modulith-foundry.purchasing";
        return host.AddRebusService(
                services =>
                {
                    services.AddLogging();
                    services.AddSalesPersistence();
                    services.AddSingleton(TimeProvider.System);
                    services.AddScoped<RecordReservationOutcomeHandler>();
                    services.AddScoped<RecordReleaseOutcomeHandler>();
                    services.AddRebusHandler<StockReservationReleaseOutcomeMessageHandler>();
                    services.AddScoped<RecordReplenishmentOutcomeHandler>();
                    services.AddRebusHandler<ReplenishmentOutcomeMessageHandler>();
                    services.AddRebusHandler<StockReservationOutcomeMessageHandler>();
                    services.AddRebus(
                        configure =>
                            configure
                                .Transport(transport =>
                                    transport
                                        .UseRabbitMq(connection, InputQueue)
                                        .SetPublisherConfirms(true)
                                        .Prefetch(4)
                                )
                                .Routing(routing =>
                                    routing
                                        .TypeBased()
                                        .Map<ReserveStockV1>(inventoryQueue)
                                        .Map<ReleaseReservationV1>(inventoryQueue)
                                        .Map<CreateReplenishmentRequirementV1>(purchasingQueue)
                                )
                                .Serialization(serializer =>
                                {
                                    serializer.UseSystemTextJson(
                                        new JsonSerializerOptions(JsonSerializerDefaults.Web)
                                        {
                                            RespectNullableAnnotations = true,
                                            Converters =
                                            {
                                                new JsonStringEnumConverter<StockReservationReleaseOutcome>(
                                                    JsonNamingPolicy.KebabCaseLower,
                                                    allowIntegerValues: false
                                                ),
                                                new JsonStringEnumConverter<StockReservationOutcome>(
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
                                        .AddWithCustomName<ReleaseReservationV1>(
                                            ReleaseReservationV1.LogicalName
                                        )
                                        .AddWithCustomName<StockReservationReleaseOutcomeV1>(
                                            StockReservationReleaseOutcomeV1.LogicalName
                                        )
                                        .AddWithCustomName<StockReservationOutcomeV1>(
                                            StockReservationOutcomeV1.LogicalName
                                        )
                                        .AddWithCustomName<CreateReplenishmentRequirementV1>(
                                            CreateReplenishmentRequirementV1.LogicalName
                                        )
                                        .AddWithCustomName<ReplenishmentRequirementCreatedV1>(
                                            ReplenishmentRequirementCreatedV1.LogicalName
                                        )
                                        .AddWithCustomName<ReplenishmentRequestRejectedV1>(
                                            ReplenishmentRequestRejectedV1.LogicalName
                                        );
                                })
                                .Options(options =>
                                {
                                    options.SetNumberOfWorkers(2);
                                    options.SetMaxParallelism(2);
                                    options.RetryStrategy(
                                        errorQueueName: ErrorQueue,
                                        maxDeliveryAttempts: 3
                                    );
                                    configureOptions?.Invoke(options);
                                }),
                        onCreated: async bus =>
                        {
                            await bus.Advanced.Topics.Subscribe(
                                StockReservationReleaseOutcomeV1.LogicalName
                            );
                            await bus.Advanced.Topics.Subscribe(
                                StockReservationOutcomeV1.LogicalName
                            );
                            await bus.Advanced.Topics.Subscribe(
                                ReplenishmentRequirementCreatedV1.LogicalName
                            );
                            await bus.Advanced.Topics.Subscribe(
                                ReplenishmentRequestRejectedV1.LogicalName
                            );
                        }
                    );
                    services.AddHostedService<SalesOutboxRelay>();
                },
                typeof(NpgsqlDataSource),
                typeof(ILoggerFactory),
                typeof(IHostApplicationLifetime)
            )
            .ConfigureServices(
                (_, services) => services.AddHostedService<PendingFulfilmentDispatcher>()
            );
    }
}
