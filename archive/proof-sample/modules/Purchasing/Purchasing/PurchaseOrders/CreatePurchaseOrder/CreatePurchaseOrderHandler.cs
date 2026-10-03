using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Purchasing.Audit;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Purchasing.Persistence;
using ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.CreatePurchaseOrder;

internal sealed class CreatePurchaseOrderHandler(
    PurchasingDbContext context,
    PurchaseOrderStore store,
    PurchaseOrderAuthorization authorization,
    TimeProvider timeProvider
)
{
    internal async Task<CreatePurchaseOrderResult> HandleAsync(
        CreatePurchaseOrderCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (
            !await authorization.AuthorizeWriteAsync(
                command.ActorUserId,
                command.OrganizationId,
                PurchasingAuditActions.PurchaseOrderCreateDenied,
                Guid.Empty,
                cancellationToken
            )
        )
            return new CreatePurchaseOrderResult.PermissionDenied();
        var now = timeProvider.GetUtcNow();
        var aggregate = PurchaseOrderAggregate.Empty(Guid.CreateVersion7(now));
        try
        {
            aggregate.Draft(command.Code, command.SupplierReference, command.Currency);
        }
        catch (InvalidPurchaseOrderValueException exception)
        {
            return new CreatePurchaseOrderResult.Invalid(exception.Field, exception.Message);
        }
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        try
        {
            await store.StageAppendAsync(
                command.OrganizationId,
                command.ActorUserId,
                aggregate,
                now,
                cancellationToken
            );
            context.AuditEntries.Add(
                PurchasingAuditEntry.RecordHuman(
                    command.OrganizationId.Value,
                    command.ActorUserId.Value,
                    PurchasingAuditActions.PurchaseOrderCreated,
                    aggregate.Id,
                    new { aggregate.Version },
                    now
                )
            );
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (PurchaseOrderStore.IsCodeConflict(exception))
        {
            context.ChangeTracker.Clear();
            return new CreatePurchaseOrderResult.CodeUnavailable();
        }
        return new CreatePurchaseOrderResult.Created(
            aggregate.State!.ToView(aggregate.Id, aggregate.Version, now)
        );
    }
}
