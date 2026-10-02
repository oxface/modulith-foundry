using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Purchasing.Messaging;
using ModulithFoundry.Modules.Purchasing.StockItemProjection;
using Npgsql;
using Rebus.Config;
using Rebus.Retry.Simple;
using Rebus.Serialization.Custom;
using Rebus.Serialization.Json;

namespace ModulithFoundry.Modules.Purchasing.Composition;

public static class PurchasingMessaging
{
    public const string InputQueue = "modulith-foundry.purchasing";
    public const string ErrorQueue = "modulith-foundry.purchasing.error";

    public static IHostBuilder AddPurchasingMessaging(
        this IHostBuilder host,
        IConfiguration configuration,
        Action<OptionsConfigurer>? configureOptions = null
    )
    {
        string connection =
            configuration.GetConnectionString("rabbitmq")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:rabbitmq is required for Purchasing messaging."
            );
        return host.AddRebusService(
                services =>
                {
                    services.AddLogging();
                    services.AddPurchasingPersistence();
                    services.AddSingleton(TimeProvider.System);
                    services.AddScoped<RecordStockItemReferenceHandler>();
                    services.AddRebusHandler<StockItemReferenceChangedMessageHandler>();
                    services.AddRebus(
                        configure =>
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
                                        }
                                    );
                                    serializer
                                        .UseCustomMessageTypeNames()
                                        .AddWithCustomName<StockItemReferenceChangedV1>(
                                            StockItemReferenceChangedV1.LogicalName
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
                        onCreated: async (bus, provider) =>
                        {
                            await bus.Advanced.Topics.Subscribe(
                                StockItemReferenceChangedV1.LogicalName
                            );
                            provider.GetRequiredService<StockItemSubscriptionBarrier>().Complete();
                        }
                    );
                },
                typeof(NpgsqlDataSource),
                typeof(ILoggerFactory),
                typeof(IHostApplicationLifetime),
                typeof(StockItemSubscriptionBarrier)
            )
            .ConfigureServices(
                (_, services) => services.AddHostedService<StockItemBootstrapWorker>()
            );
    }
}
