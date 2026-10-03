using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Migrator;
using ModulithFoundry.Modules.Access.Composition;
using ModulithFoundry.Modules.Inventory.Composition;
using ModulithFoundry.Modules.Purchasing.Composition;
using ModulithFoundry.Modules.Sales.Composition;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("database")))
{
    await Console.Error.WriteLineAsync(
        "{\"LogLevel\":\"Critical\",\"Message\":\"Connection string 'database' is required.\"}"
    );
    return 1;
}

builder.AddPostgresDataSource("database");
builder.Services.AddAccessPersistence();
builder.Services.AddInventoryPersistence();
builder.Services.AddPurchasingPersistence();
builder.Services.AddSalesPersistence();
builder.Services.AddSingleton<PostgresMigrationLock>();
builder.Services.AddSingleton<MigrationCoordinator>();

using IHost host = builder.Build();
ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Migrator");

try
{
    await host.StartAsync();
    CancellationToken stoppingToken = host
        .Services.GetRequiredService<IHostApplicationLifetime>()
        .ApplicationStopping;

    await using PostgresMigrationLock.Lease migrationLock = await host
        .Services.GetRequiredService<PostgresMigrationLock>()
        .AcquireAsync(stoppingToken);

    await host.Services.GetRequiredService<MigrationCoordinator>().MigrateAsync(stoppingToken);

    MigrationLogs.MigrationsCompleted(logger);
    return 0;
}
catch (Exception exception)
{
    MigrationLogs.MigrationFailed(logger, exception);
    return 1;
}
finally
{
    await host.StopAsync();
}
