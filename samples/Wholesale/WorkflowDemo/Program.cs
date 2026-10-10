using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModulithFoundry.Samples.Wholesale.Inventory;
using ModulithFoundry.Samples.Wholesale.Inventory.Contracts;
using ModulithFoundry.Samples.Wholesale.Sales;
using ModulithFoundry.Samples.Wholesale.Sales.Contracts;
using ModulithFoundry.Samples.Wholesale.ServiceDefaults;
using ModulithFoundry.Samples.Wholesale.WorkflowDemo;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Rootbolt.ActorIdentity;
using Rootbolt.Messaging.EntityFrameworkCore;
using Rootbolt.Messaging.EntityFrameworkCore.Postgres;
using Rootbolt.Tenancy;

var builder = Host.CreateApplicationBuilder(args);
string role = builder.Configuration["role"] ?? "run";
string sales = Required("ConnectionStrings:Sales");
string inventory = Required("ConnectionStrings:Inventory");
builder.Services.AddWorkflowDemo(sales, inventory);
using var startup = new CancellationTokenSource(TimeSpan.FromSeconds(60));

if (role is "start" or "read" or "seed")
{
    using var operations = builder.Build();
    await using var scope = operations.Services.CreateAsyncScope();
    scope
        .ServiceProvider.GetRequiredService<ITenantContextInitializer>()
        .Initialize(
            TenantContext.ForTenant(
                new TenantId(builder.Configuration["Organization"] ?? "wholesale-alpha")
            )
        );
    scope
        .ServiceProvider.GetRequiredService<IActorContextInitializer>()
        .Initialize(new ActorContext(Actor.System(new ActorId("sales.workflow-demo"))));
    var requests = scope.ServiceProvider.GetRequiredService<IStockIssueRequests>();
    if (role == "seed")
    {
        var database = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(startup.Token);
        var commands = scope.ServiceProvider.GetRequiredService<IStockPositionCommands>();
        Guid stock = Guid.Parse(Required("StockId"));
        var opened = await commands.OpenAsync(
            new(stock, Guid.NewGuid(), Guid.NewGuid(), "EA"),
            startup.Token
        );
        if (opened is not StockPositionChangeResult.Changed)
            throw new InvalidOperationException("Seed needs a new stock identity.");

        await database.SaveChangesAsync(startup.Token);
        await transaction.CommitAsync(startup.Token);
        await using var receiptScope = operations.Services.CreateAsyncScope();
        receiptScope
            .ServiceProvider.GetRequiredService<ITenantContextInitializer>()
            .Initialize(
                TenantContext.ForTenant(
                    new TenantId(builder.Configuration["Organization"] ?? "wholesale-alpha")
                )
            );
        var receiptDatabase = receiptScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await using var receipt = await receiptDatabase.Database.BeginTransactionAsync(
            startup.Token
        );
        var received = await receiptScope
            .ServiceProvider.GetRequiredService<IStockPositionCommands>()
            .ReceiveAsync(new(stock, 1, [new StockReceipt(5)]), startup.Token);
        if (received is not StockPositionChangeResult.Changed)
            throw new InvalidOperationException("The seed receipt did not change stock.");

        await receiptDatabase.SaveChangesAsync(startup.Token);
        await receipt.CommitAsync(startup.Token);
        Console.WriteLine("Stock seeded at version 2 with quantity 5.");
    }
    else
    {
        Guid request = Guid.Parse(Required("RequestId"));
        var progress =
            role == "read"
                ? await requests.ReadAsync(request, startup.Token)
                : await requests.StartAsync(
                    new(
                        request,
                        Guid.Parse(Required("StockId")),
                        builder.Configuration.GetValue("ExpectedVersion", 2L),
                        builder.Configuration.GetValue("Quantity", 1m),
                        DateTimeOffset.Parse(
                            Required("ReplyDeadline"),
                            System.Globalization.CultureInfo.InvariantCulture
                        )
                    ),
                    startup.Token
                );
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(progress));
    }

    return;
}

await using var broker = await WorkflowBroker.OpenAsync(
    Required("ConnectionStrings:RabbitMq"),
    builder.Configuration["QueuePrefix"] ?? "wf1",
    startup.Token
);
builder.Services.AddSingleton(broker);
if (role == "setup")
{
    using var setup = builder.Build();
    await using var scope = setup.Services.CreateAsyncScope();
    await scope
        .ServiceProvider.GetRequiredService<SalesDbContext>()
        .Database.MigrateAsync(startup.Token);
    await scope
        .ServiceProvider.GetRequiredService<InventoryDbContext>()
        .Database.MigrateAsync(startup.Token);
    await broker.SetupAsync(startup.Token);
    Console.WriteLine("Workflow schemas and queues ready.");
    return;
}

if (role != "run")
    throw new InvalidOperationException("Choose --role setup, seed, start, read or run.");

// Fail before hosting if explicit setup has not installed the required storage/topology.
// These privileged probes check structure only; they do not expose tenant business data.
await broker.RequireQueuesAsync(startup.Token);
builder.AddServiceDefaults();
builder
    .Services.AddOpenTelemetry()
    .WithTracing(tracing =>
        tracing
            .AddRootboltMessaging()
            .AddSource("RabbitMQ.Client.Publisher", "RabbitMQ.Client.Subscriber")
    )
    .WithMetrics(metrics => metrics.AddRootboltMessaging());
builder.Services.AddPostgresOutboxDispatcher<SalesDbContext, SalesCommandPublisher>(
    new(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1))
);
builder.Services.AddPostgresOutboxDispatcher<InventoryDbContext, InventoryReplyPublisher>(
    new(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1))
);
builder.Services.AddOutboxWorker<SalesDbContext>(
    new(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1))
);
builder.Services.AddOutboxWorker<InventoryDbContext>(
    new(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1))
);
builder.Services.AddInboxWorker<SalesDbContext>(
    StockIssueReplyAdmission.Subscription,
    new(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1))
);
builder.Services.AddInboxWorker<InventoryDbContext>(
    StockIssueMessageAdmission.Subscription,
    new(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1))
);
builder.Services.AddHostedService<WorkflowIntakeWorker>();
builder.Services.AddHostedService<StockIssueDeadlineWorker>();
using var host = builder.Build();
await using (var scope = host.Services.CreateAsyncScope())
{
    var salesDatabase = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
    var inventoryDatabase = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    _ = await salesDatabase.Set<InboxMessageRecord>().IgnoreQueryFilters().AnyAsync(startup.Token);
    _ = await salesDatabase.Set<OutboxMessageRecord>().IgnoreQueryFilters().AnyAsync(startup.Token);
    _ = await inventoryDatabase
        .Set<InboxMessageRecord>()
        .IgnoreQueryFilters()
        .AnyAsync(startup.Token);
    _ = await inventoryDatabase
        .Set<OutboxMessageRecord>()
        .IgnoreQueryFilters()
        .AnyAsync(startup.Token);
    _ = await salesDatabase
        .Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\" FROM sales.stock_issue_requests")
        .Take(1)
        .ToArrayAsync(startup.Token);
}

await host.RunAsync();

string Required(string key) =>
    builder.Configuration[key] ?? throw new InvalidOperationException("Configure " + key + ".");
