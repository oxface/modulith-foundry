using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Messaging;
using ModulithFoundry.Modules.Inventory.Reservations;
using ModulithFoundry.Modules.Inventory.StockPositions.Persistence;
using Npgsql;
using Rebus.Config;
using Rebus.Retry.Simple;
using Rebus.Serialization.Custom;
using Rebus.Serialization.Json;

namespace ModulithFoundry.Modules.Inventory.Composition;

public static class InventoryMessaging
{
    public const string InputQueue = "modulith-foundry.inventory";
    public const string ErrorQueue = "modulith-foundry.inventory.error";
    public const string MeterName = "ModulithFoundry.Inventory.Messaging";

    public static IHostBuilder AddInventoryMessaging(
        this IHostBuilder host,
        IConfiguration configuration,
        Action<OptionsConfigurer>? configureOptions = null
    )
    {
        string connection =
            configuration.GetConnectionString("rabbitmq")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:rabbitmq is required for Inventory messaging."
            );
        // Generic Host owns IMeterFactory; share one module instrument set with the isolated endpoint.
        host.ConfigureServices(
            (_, services) => services.TryAddSingleton<InventoryMessagingMetrics>()
        );
        return host.AddRebusService(
            services =>
            {
                services.AddLogging();
                services.AddInventoryPersistence();
                services.AddSingleton(TimeProvider.System);
                services.AddScoped<StockPositionStore>();
                services.AddScoped<StockPositionWriteGate>();
                services.AddScoped<StockPositionInlineProjection>();
                services.AddScoped<StockPositionEventReader>();
                services.AddScoped<ReserveStockHandler>();
                services.AddScoped<ReleaseReservationHandler>();
                services.AddRebusHandler<ReserveStockMessageHandler>();
                services.AddRebusHandler<ReleaseReservationMessageHandler>();
                services.AddRebus(configure =>
                    configure
                        .Transport(transport =>
                            transport
                                .UseRabbitMq(connection, InputQueue)
                                .SetPublisherConfirms(true)
                                .Prefetch(4)
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
                                .AddWithCustomName<ReserveStockV1>(ReserveStockV1.LogicalName)
                                .AddWithCustomName<StockReservationOutcomeV1>(
                                    StockReservationOutcomeV1.LogicalName
                                )
                                .AddWithCustomName<StockItemReferenceChangedV1>(
                                    StockItemReferenceChangedV1.LogicalName
                                )
                                .AddWithCustomName<ReleaseReservationV1>(
                                    ReleaseReservationV1.LogicalName
                                )
                                .AddWithCustomName<StockReservationReleaseOutcomeV1>(
                                    StockReservationReleaseOutcomeV1.LogicalName
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
                        })
                );
                services.AddHostedService<InventoryOutboxRelay>();
                services.AddHostedService<InventoryOutboxMonitor>();
            },
            typeof(NpgsqlDataSource),
            typeof(ILoggerFactory),
            typeof(InventoryMessagingMetrics),
            typeof(IHostApplicationLifetime)
        );
    }
}
