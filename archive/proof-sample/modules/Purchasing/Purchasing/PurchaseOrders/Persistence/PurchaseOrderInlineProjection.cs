using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Purchasing.Persistence;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

internal sealed class PurchaseOrderInlineProjection(PurchasingDbContext context)
{
    internal async Task StageAsync(
        Guid organizationId,
        PurchaseOrderAggregate aggregate,
        CancellationToken cancellationToken
    )
    {
        var model = await context.PurchaseOrderWriteModels.SingleOrDefaultAsync(
            x => x.OrganizationId == organizationId && x.StreamId == aggregate.Id,
            cancellationToken
        );
        var summary = await context.PurchaseOrderSummaries.SingleOrDefaultAsync(
            x => x.OrganizationId == organizationId && x.StreamId == aggregate.Id,
            cancellationToken
        );
        if (aggregate.ExpectedVersion == 0)
        {
            if (model is not null || summary is not null)
                throw new PurchaseOrderIntegrityException(
                    aggregate.Id,
                    PurchaseOrderIntegrityFailure.UnexpectedInlineModel
                );
            model = PurchaseOrderWriteModel.Create(aggregate.Id, organizationId);
            summary = PurchaseOrderSummary.Create(aggregate.Id, organizationId);
            context.PurchaseOrderWriteModels.Add(model);
            context.PurchaseOrderSummaries.Add(summary);
        }
        else if (
            model is null
            || summary is null
            || model.Version < aggregate.ExpectedVersion
            || summary.Version < aggregate.ExpectedVersion
        )
            throw new PurchaseOrderIntegrityException(
                aggregate.Id,
                PurchaseOrderIntegrityFailure.InlineModelBehind
            );
        if (
            model.Version > aggregate.ExpectedVersion
            || summary.Version > aggregate.ExpectedVersion
        )
            throw new PurchaseOrderConcurrencyException();
        PurchaseOrderState? state = aggregate.ExpectedVersion == 0 ? null : model.ToState();
        foreach (var @event in aggregate.Pending)
        {
            summary.Apply(@event);
            state = PurchaseOrderEvolution.Evolve(state, @event);
        }
        model.Apply(
            state
                ?? throw new PurchaseOrderIntegrityException(
                    aggregate.Id,
                    PurchaseOrderIntegrityFailure.EmptyState
                ),
            aggregate.Version
        );
    }
}
