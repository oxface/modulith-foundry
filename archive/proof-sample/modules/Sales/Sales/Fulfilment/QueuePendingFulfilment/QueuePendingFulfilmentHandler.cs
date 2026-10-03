using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Fulfilment.QueuePendingFulfilment;

internal sealed class QueuePendingFulfilmentHandler(
    SalesDbContext context,
    IStockingLocationReferenceResolver locations,
    TimeProvider timeProvider
)
{
    internal async Task QueueAsync(
        Guid organizationId,
        Guid processId,
        CancellationToken cancellationToken
    )
    {
        context.UseWorkflowOrganization(organizationId);
        StockingLocationId? location = await locations.FindActiveAsync(
            new OrganizationId(organizationId),
            ReservationCommandStaging.DefaultLocationCode,
            cancellationToken
        );
        if (location is null)
            return;
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        OrderFulfilmentProcess? process = await context.FulfilmentProcesses.SingleOrDefaultAsync(
            item => item.Id == processId,
            cancellationToken
        );
        if (process is null || process.Status != OrderFulfilmentStatus.PendingDispatch)
            return;
        var order = await context.SalesOrders.SingleAsync(
            item => item.Id == process.OrderId,
            cancellationToken
        );
        DateTimeOffset timestamp = timeProvider.GetUtcNow();
        DateTimeOffset now = new(timestamp.UtcTicks - timestamp.UtcTicks % 10, TimeSpan.Zero);
        ReservationCommandStaging.Stage(context, process, order, location.Value.Value, now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
