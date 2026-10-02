using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Fulfilment.QueuePendingFulfilment;

internal sealed class QueuePendingReplenishmentHandler(SalesDbContext context, TimeProvider clock)
{
    internal async Task QueueAsync(
        Guid organizationId,
        Guid processId,
        CancellationToken cancellationToken
    )
    {
        context.UseWorkflowOrganization(organizationId);
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        var process = await context.FulfilmentProcesses.SingleOrDefaultAsync(
            x => x.Id == processId,
            cancellationToken
        );
        if (process is null)
            return;
        var time = clock.GetUtcNow();
        DateTimeOffset now = new(time.UtcTicks - time.UtcTicks % 10, TimeSpan.Zero);
        foreach (var line in process.Lines)
            ReplenishmentCommandStaging.Stage(context, process, line, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
