using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Messaging;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Fulfilment.QueuePendingFulfilment;

internal sealed class PendingFulfilmentDispatcher(
    IServiceScopeFactory scopes,
    ILogger<PendingFulfilmentDispatcher> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await QueuePendingAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                SalesMessagingLogs.PendingDispatchFailed(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task QueuePendingAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope discovery = scopes.CreateAsyncScope();
        var pending = await discovery
            .ServiceProvider.GetRequiredService<SalesDbContext>()
            .FulfilmentProcesses.IgnoreQueryFilters([SalesDbContext.OrganizationScopeFilter])
            .AsNoTracking()
            .Where(process =>
                !process.CancellationRequested
                && (
                    process.Status == OrderFulfilmentStatus.PendingDispatch
                    || process.Lines.Any(line =>
                        line.Status == OrderFulfilmentLineStatus.Shortage
                        && line.ReplenishmentCommandMessageId == null
                    )
                )
            )
            .OrderBy(process => process.CreatedAt)
            .ThenBy(process => process.Id)
            .Select(process => new
            {
                process.OrganizationId,
                process.Id,
                process.Status,
            })
            .ToArrayAsync(cancellationToken);
        foreach (var item in pending)
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            try
            {
                if (item.Status == OrderFulfilmentStatus.PendingDispatch)
                    await scope
                        .ServiceProvider.GetRequiredService<QueuePendingFulfilmentHandler>()
                        .QueueAsync(item.OrganizationId, item.Id, cancellationToken);
                else
                    await scope
                        .ServiceProvider.GetRequiredService<QueuePendingReplenishmentHandler>()
                        .QueueAsync(item.OrganizationId, item.Id, cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            { /* Another dispatcher won. This scope is discarded. */
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One damaged process must not prevent other tenants' approved work from progressing.
                SalesMessagingLogs.ProcessDispatchFailed(logger, item.Id, exception);
            }
        }
    }
}
