using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.BrokerReceiver;
using ModulithFoundry.Modules.Inventory.Composition;
using ModulithFoundry.Modules.Purchasing.Composition;
using ModulithFoundry.Modules.Sales.Composition;
using Npgsql;
using Rebus.Config;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;

IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
bool pauseAfterCommit = configuration["ReceiverTest:PauseAfterCommit"] == "true";
bool purchasing = configuration["ReceiverTest:Module"] == "purchasing";
bool sales = configuration["ReceiverTest:Module"] == "sales";
bool pauseSnapshot = configuration["ReceiverTest:PauseSnapshot"] == "true";
bool pauseReceiptRead = configuration["ReceiverTest:PauseReceiptRead"] == "true";
using var receiptCheckpoint = new ReceiptReadCheckpoint();
using var shutdownCheckpoint = new CancellationTokenSource();
IHostBuilder builder = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        if (pauseReceiptRead)
            logging
                .AddProvider(receiptCheckpoint)
                .AddFilter<ReceiptReadCheckpoint>(
                    "Microsoft.EntityFrameworkCore.Database.Command",
                    LogLevel.Information
                );
        if (pauseSnapshot)
            logging
                .AddProvider(new SnapshotCheckpoint())
                .AddFilter<SnapshotCheckpoint>(
                    "Microsoft.EntityFrameworkCore.Database.Command",
                    LogLevel.Information
                );
    })
    .ConfigureServices(services =>
    {
        services.AddSingleton(_ =>
            NpgsqlDataSource.Create(
                configuration.GetConnectionString("database")
                    ?? throw new InvalidOperationException("A test database is required.")
            )
        );
        if (purchasing)
        {
            // Endpoint cutover proof only: the exporter remains an in-process Inventory Contract.
            // This is not a separately deployable Purchasing service.
            services.AddInventoryModule();
            services.AddPurchasingModule();
        }
        if (sales)
        {
            services.AddInventoryModule();
            services.AddSalesModule();
        }
    });
Action<OptionsConfigurer> checkpoints = options =>
    options.Decorate<IPipeline>(context =>
        new PipelineStepInjector(context.Get<IPipeline>()).OnReceive(
            new SettlementCheckpoint(pauseAfterCommit, shutdownCheckpoint.Token),
            PipelineRelativePosition.Before,
            typeof(DispatchIncomingMessageStep)
        )
    );
if (purchasing)
    builder.AddPurchasingMessaging(configuration, checkpoints);
else if (sales)
    builder.AddSalesMessaging(configuration, checkpoints);
else
    builder.AddInventoryMessaging(configuration, checkpoints);
using IHost host = builder.Build();
using var shutdownRegistration = host
    .Services.GetRequiredService<IHostApplicationLifetime>()
    .ApplicationStopping.Register(shutdownCheckpoint.Cancel);

await host.StartAsync();
Console.WriteLine("ready");

// A private test control channel requests the real Generic Host shutdown; no production hook.
_ = Task.Run(async () =>
{
    while (await Console.In.ReadLineAsync() is { } command)
    {
        if (command == "release")
            receiptCheckpoint.Release();
        if (command == "stop")
        {
            receiptCheckpoint.Release();
            host.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
            return;
        }
    }
});
await host.WaitForShutdownAsync();
