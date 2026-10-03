using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Purchasing.Messaging;
using ModulithFoundry.Modules.Purchasing.Persistence;

namespace ModulithFoundry.Modules.Purchasing.StockItemProjection;

internal sealed class StockItemProjectionMonitor(
    IServiceScopeFactory scopes,
    PurchasingMessagingMetrics metrics,
    ILogger<StockItemProjectionMonitor> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<PurchasingDbContext>();
                context.Database.SetCommandTimeout(5);
                // Bootstrap is module-wide technical state, not an Organization-owned business row.
                var checkpoint = await context
                    .StockItemBootstrapCheckpoints.AsNoTracking()
                    .SingleAsync(stoppingToken);
                metrics.ObserveProjection(checkpoint.IsReady);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                metrics.ProjectionObservationFailed();
                PurchasingMessagingLogs.ProjectionObservationFailed(
                    logger,
                    exception.GetType().Name
                );
            }
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
