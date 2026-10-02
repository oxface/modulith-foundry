using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Purchasing.Audit;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Purchasing.Persistence;
using ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.SetPurchaseOrderLine;

internal sealed class SetPurchaseOrderLineHandler(
    PurchasingDbContext context,
    PurchaseOrderStore store,
    PurchaseOrderAuthorization authorization,
    TimeProvider timeProvider
)
{
    internal async Task<SetPurchaseOrderLineResult> HandleAsync(
        SetPurchaseOrderLineCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (
            !await authorization.AuthorizeWriteAsync(
                command.ActorUserId,
                command.OrganizationId,
                PurchasingAuditActions.PurchaseOrderLineSetDenied,
                command.PurchaseOrderId,
                cancellationToken
            )
        )
            return new SetPurchaseOrderLineResult.PermissionDenied();
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        try
        {
            var aggregate = await store.LoadForWritingAsync(
                command.OrganizationId,
                command.PurchaseOrderId,
                command.ExpectedVersion,
                cancellationToken
            );
            if (aggregate is null)
                return new SetPurchaseOrderLineResult.NotFound();
            aggregate.SetLine(command.ItemCode, command.Quantity, command.UnitPrice);
            if (aggregate.Pending.Count == 0)
            {
                var stream = await context.EventStreams.SingleAsync(
                    x => x.OrganizationId == command.OrganizationId.Value && x.Id == aggregate.Id,
                    cancellationToken
                );
                return new SetPurchaseOrderLineResult.Unchanged(
                    aggregate.State!.ToView(aggregate.Id, aggregate.Version, stream.UpdatedAt)
                );
            }
            var now = timeProvider.GetUtcNow();
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
                    PurchasingAuditActions.PurchaseOrderLineSet,
                    aggregate.Id,
                    new { aggregate.Version },
                    now
                )
            );
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new SetPurchaseOrderLineResult.Changed(
                aggregate.State!.ToView(aggregate.Id, aggregate.Version, now)
            );
        }
        catch (InvalidPurchaseOrderValueException exception)
        {
            return new SetPurchaseOrderLineResult.Invalid(exception.Field, exception.Message);
        }
        catch (PurchaseOrderDecisionException exception)
        {
            return new SetPurchaseOrderLineResult.Rejected(exception.Message);
        }
        catch (PurchaseOrderConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return new SetPurchaseOrderLineResult.VersionConflict();
        }
        catch (DbUpdateException exception) when (PurchaseOrderStore.IsVersionConflict(exception))
        {
            context.ChangeTracker.Clear();
            return new SetPurchaseOrderLineResult.VersionConflict();
        }
    }
}
