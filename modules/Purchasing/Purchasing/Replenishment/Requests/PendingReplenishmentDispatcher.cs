using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModulithFoundry.Modules.Purchasing.Messaging;
using ModulithFoundry.Modules.Purchasing.Persistence;

namespace ModulithFoundry.Modules.Purchasing.Replenishment.Requests;

internal sealed class PendingReplenishmentDispatcher(
    IServiceScopeFactory scopes,
    TimeProvider timeProvider,
    ILogger<PendingReplenishmentDispatcher> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                PurchasingMessagingLogs.PendingScanFailed(logger, exception);
            }
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scan = scopes.CreateAsyncScope();
        var context = scan.ServiceProvider.GetRequiredService<PurchasingDbContext>();
        DateTimeOffset now = timeProvider.GetUtcNow();
        var due = await context
            .ReplenishmentRequests.IgnoreQueryFilters([PurchasingDbContext.OrganizationScopeFilter])
            .AsNoTracking()
            .Where(x => x.RequirementId == null && x.ReasonCode == null && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt)
            .ThenBy(x => x.OperationId)
            .Take(10)
            .Select(x => new { x.OrganizationId, x.OperationId })
            .ToListAsync(cancellationToken);
        foreach (var request in due)
        {
            await using AsyncServiceScope operation = scopes.CreateAsyncScope();
            await operation
                .ServiceProvider.GetRequiredService<ResumeReplenishmentRequestHandler>()
                .HandleAsync(request.OrganizationId, request.OperationId, cancellationToken);
        }
    }
}
