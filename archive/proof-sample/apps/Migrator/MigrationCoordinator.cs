using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Access.Composition;
using ModulithFoundry.Modules.Inventory.Composition;
using ModulithFoundry.Modules.Purchasing.Composition;
using ModulithFoundry.Modules.Sales.Composition;

namespace ModulithFoundry.Migrator;

internal sealed class MigrationCoordinator(
    IServiceProvider services,
    ILogger<MigrationCoordinator> logger
)
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        MigrationLogs.ApplyingModule(logger, "Access");
        await services.MigrateAccessAsync(cancellationToken);

        MigrationLogs.ApplyingModule(logger, "Inventory");
        await services.MigrateInventoryAsync(cancellationToken);

        MigrationLogs.ApplyingModule(logger, "Purchasing");
        await services.MigratePurchasingAsync(cancellationToken);

        MigrationLogs.ApplyingModule(logger, "Sales");
        await services.MigrateSalesAsync(cancellationToken);
    }
}
