using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Purchasing.Persistence;

namespace ModulithFoundry.Modules.Purchasing.Replenishment.Requests;

internal sealed class ResumeReplenishmentRequestHandler(
    PurchasingDbContext context,
    ReplenishmentRequestProcessor processor,
    TimeProvider timeProvider
)
{
    internal async Task HandleAsync(
        Guid organizationId,
        Guid operationId,
        CancellationToken cancellationToken
    )
    {
        context.UseWorkflowOrganization(organizationId);
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        var checkpoint = (
            await context
                .StockItemBootstrapCheckpoints.FromSqlRaw(
                    "SELECT * FROM purchasing.stock_item_bootstrap WHERE id = 1 FOR UPDATE"
                )
                .ToListAsync(cancellationToken)
        ).Single();
        ReplenishmentRequest? request = await context.ReplenishmentRequests.SingleOrDefaultAsync(
            x => x.OperationId == operationId && x.OrganizationId == organizationId,
            cancellationToken
        );
        DateTimeOffset timestamp = timeProvider.GetUtcNow();
        DateTimeOffset now = new(timestamp.UtcTicks - timestamp.UtcTicks % 10, TimeSpan.Zero);
        if (request is null || !request.IsPending || request.NextAttemptAt > now)
            return;
        await processor.ResolveAsync(request, checkpoint.IsReady, now, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
