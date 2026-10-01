using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.BrokerReceiver;
using ModulithFoundry.Modules.Inventory.Composition;
using Npgsql;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;

IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
bool pauseAfterCommit = configuration["ReceiverTest:PauseAfterCommit"] == "true";
using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging => logging.ClearProviders())
    .ConfigureServices(services =>
        services.AddSingleton(_ =>
            NpgsqlDataSource.Create(
                configuration.GetConnectionString("database")
                    ?? throw new InvalidOperationException("A test database is required.")
            )
        )
    )
    .AddInventoryMessaging(
        configuration,
        options =>
            options.Decorate<IPipeline>(context =>
                new PipelineStepInjector(context.Get<IPipeline>()).OnReceive(
                    new SettlementCheckpoint(pauseAfterCommit),
                    PipelineRelativePosition.Before,
                    typeof(DispatchIncomingMessageStep)
                )
            )
    )
    .Build();

await host.StartAsync();
Console.WriteLine("ready");
await host.WaitForShutdownAsync();
