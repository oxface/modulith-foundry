using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Purchasing.Audit;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Purchasing.Persistence;
using ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.IssuePurchaseOrder;

internal sealed class IssuePurchaseOrderHandler(
    PurchasingDbContext context,
    PurchaseOrderStore store,
    PurchaseOrderAuthorization authorization,
    TimeProvider timeProvider
) : IPurchaseOrderIssuance
{
    public async Task<IssuePurchaseOrderResult> IssueAsync(
        IssuePurchaseOrderCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (
            !await authorization.AuthorizeWriteAsync(
                command.ActorUserId,
                command.OrganizationId,
                PurchasingAuditActions.PurchaseOrderIssueDenied,
                command.PurchaseOrderId,
                cancellationToken
            )
        )
            return new IssuePurchaseOrderResult.PermissionDenied();
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
                return new IssuePurchaseOrderResult.NotFound();
            aggregate.Issue();
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
                    PurchasingAuditActions.PurchaseOrderIssued,
                    aggregate.Id,
                    new { aggregate.Version },
                    now
                )
            );
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new IssuePurchaseOrderResult.Issued(
                aggregate.State!.ToView(aggregate.Id, aggregate.Version, now)
            );
        }
        catch (InvalidPurchaseOrderValueException exception)
        {
            return new IssuePurchaseOrderResult.Invalid(exception.Field, exception.Message);
        }
        catch (PurchaseOrderDecisionException exception)
        {
            return new IssuePurchaseOrderResult.Rejected(exception.Message);
        }
        catch (PurchaseOrderConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return new IssuePurchaseOrderResult.VersionConflict();
        }
        catch (DbUpdateException exception) when (PurchaseOrderStore.IsVersionConflict(exception))
        {
            context.ChangeTracker.Clear();
            return new IssuePurchaseOrderResult.VersionConflict();
        }
    }
}
