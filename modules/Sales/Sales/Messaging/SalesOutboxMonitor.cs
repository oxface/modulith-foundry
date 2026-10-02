using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Messaging;

internal sealed class SalesOutboxMonitor(
    IServiceScopeFactory scopes,
    SalesMessagingMetrics metrics,
    ILogger<SalesOutboxMonitor> logger
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
                // Privileged read-only observation across Organizations, restricted to this module's schema.
                var sample = await context
                    .Database.SqlQuery<OutboxObservation>(
                        $"""
                        SELECT count(*) AS "Pending",
                            count(*) FILTER (WHERE lease_until <= clock_timestamp()) AS "ExpiredLeases",
                            COALESCE(GREATEST(EXTRACT(EPOCH FROM clock_timestamp() - min(created_at)), 0), 0)::double precision AS "OldestAge"
                        FROM sales.outbox_messages WHERE dispatched_at IS NULL
                        """
                    )
                    .SingleAsync(stoppingToken);
                metrics.Observe(sample.Pending, sample.OldestAge, sample.ExpiredLeases);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Retain the last successful sample; never turn an unavailable source into zero backlog.
                metrics.ObservationFailed();
                SalesMessagingLogs.ObservationFailed(logger, exception.GetType().Name);
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

    private sealed class OutboxObservation
    {
        public long Pending { get; set; }
        public long ExpiredLeases { get; set; }
        public double OldestAge { get; set; }
    }
}
