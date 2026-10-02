using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.BrokerReceiver;
using ModulithFoundry.Modules.Inventory.Composition;
using ModulithFoundry.Modules.Purchasing.Composition;
using Npgsql;
using Rebus.Config;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;

IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
bool pauseAfterCommit = configuration["ReceiverTest:PauseAfterCommit"] == "true";
bool purchasing = configuration["ReceiverTest:Module"] == "purchasing";
bool pauseSnapshot = configuration["ReceiverTest:PauseSnapshot"] == "true";
IHostBuilder builder = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
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
    });
Action<OptionsConfigurer> checkpoints = options =>
    options.Decorate<IPipeline>(context =>
        new PipelineStepInjector(context.Get<IPipeline>()).OnReceive(
            new SettlementCheckpoint(pauseAfterCommit),
            PipelineRelativePosition.Before,
            typeof(DispatchIncomingMessageStep)
        )
    );
if (purchasing)
    builder.AddPurchasingMessaging(configuration, checkpoints);
else
    builder.AddInventoryMessaging(configuration, checkpoints);
using IHost host = builder.Build();

await host.StartAsync();
Console.WriteLine("ready");
await host.WaitForShutdownAsync();
