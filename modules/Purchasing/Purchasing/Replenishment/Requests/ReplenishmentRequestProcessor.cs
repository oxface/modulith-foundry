using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Purchasing.Audit;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Purchasing.Messaging.Persistence;
using ModulithFoundry.Modules.Purchasing.Persistence;

namespace ModulithFoundry.Modules.Purchasing.Replenishment.Requests;

internal sealed class ReplenishmentRequestProcessor(PurchasingDbContext context)
{
    // Caller owns the Purchasing transaction and checkpoint lock through both saves.
    internal async Task ResolveAsync(
        ReplenishmentRequest request,
        bool projectionReady,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        if (!request.IsPending)
            return;
        var reference = projectionReady
            ? await context
                .StockItemReferences.IgnoreQueryFilters([
                    PurchasingDbContext.OrganizationScopeFilter,
                ])
                .SingleOrDefaultAsync(
                    x =>
                        x.OrganizationId == request.OrganizationId
                        && x.StockItemId == request.StockItemId,
                    cancellationToken
                )
            : null;
        if (reference is not null && reference.SourceRevision < request.MinimumReferenceRevision)
        {
            request.Wait(now);
            return;
        }
        string? rejection = null;
        if (
            projectionReady
            && reference is null
            && await context
                .StockItemReferences.IgnoreQueryFilters([
                    PurchasingDbContext.OrganizationScopeFilter,
                ])
                .AnyAsync(
                    x =>
                        x.StockItemId == request.StockItemId
                        && x.OrganizationId != request.OrganizationId,
                    cancellationToken
                )
        )
            rejection = ReplenishmentRequestReasonCodes.ForeignStockItem;
        else if (reference is not null && reference.BaseUnitCode != request.BaseUnitCode)
            rejection = ReplenishmentRequestReasonCodes.BaseUnitMismatch;
        else if (reference is not null && !reference.IsActive)
            rejection = ReplenishmentRequestReasonCodes.InactiveStockItem;
        if (rejection is not null)
        {
            request.Reject(rejection);
            context.AuditEntries.Add(
                PurchasingAuditEntry.Record(
                    request.OrganizationId,
                    PurchasingAuditActions.RequestRejected,
                    request.OperationId,
                    PurchasingAuditOutcomes.Rejected,
                    rejection,
                    new { request.ProcessId, request.LineNumber },
                    now
                )
            );
            return;
        }
        if (reference is null)
        {
            request.Wait(now);
            return;
        }
        var requirement = ReplenishmentRequirement.Create(
            request.OrganizationId,
            request.OperationId,
            ReplenishmentQuantity.Create(request.Quantity),
            new(
                reference.StockItemId,
                reference.Sku,
                reference.Description,
                reference.BaseUnitCode,
                reference.SourceRevision
            ),
            now
        );
        context.Requirements.Add(requirement);
        request.Complete(requirement.Id);
        // Obtain the short PostgreSQL-generated number; the enclosing transaction is not committed.
        await context.SaveChangesAsync(cancellationToken);
        context.AuditEntries.Add(
            PurchasingAuditEntry.Record(
                request.OrganizationId,
                PurchasingAuditActions.RequirementCreated,
                requirement.Id,
                PurchasingAuditOutcomes.Succeeded,
                null,
                new
                {
                    request.OperationId,
                    request.ProcessId,
                    request.LineNumber,
                    requirement.ReferenceRevision,
                },
                now,
                subjectType: PurchasingAuditSubjects.Requirement
            )
        );
        context.OutboxMessages.Add(
            PurchasingOutboxMessage.Stage(
                new(
                    Guid.CreateVersion7(now),
                    request.FirstMessageId,
                    request.OrganizationId,
                    request.OperationId,
                    request.ProcessId,
                    request.OrderNumber,
                    request.LineNumber,
                    request.StockItemId,
                    requirement.Id,
                    requirement.Number,
                    request.Quantity,
                    request.BaseUnitCode,
                    now
                )
            )
        );
    }
}
