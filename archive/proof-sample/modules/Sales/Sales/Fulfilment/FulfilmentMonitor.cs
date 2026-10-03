using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Messaging;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Fulfilment;

internal sealed class FulfilmentMonitor(
    IServiceScopeFactory scopes,
    SalesMessagingMetrics metrics,
    ILogger<FulfilmentMonitor> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
                context.Database.SetCommandTimeout(5);
                // Privileged observation only of this module's process state, across Organizations.
                var sample = await context
                    .Database.SqlQuery<ProcessObservation>(
                        $"""
                        SELECT count(*) AS "Unsettled",
                            COALESCE(GREATEST(EXTRACT(EPOCH FROM clock_timestamp() - min(created_at)), 0), 0)::double precision AS "OldestAge"
                        FROM sales.fulfilment_processes
                        WHERE status NOT IN ({OrderFulfilmentStatusValues.Reserved}, {OrderFulfilmentStatusValues.Compensated})
                        """
                    )
                    .SingleAsync(stoppingToken);
                metrics.ObserveProcesses(sample.Unsettled, sample.OldestAge);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                metrics.ProcessObservationFailed();
                SalesMessagingLogs.ProcessObservationFailed(logger, exception.GetType().Name);
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

    private sealed class ProcessObservation
    {
        public long Unsettled { get; set; }
        public double OldestAge { get; set; }
    }
}
