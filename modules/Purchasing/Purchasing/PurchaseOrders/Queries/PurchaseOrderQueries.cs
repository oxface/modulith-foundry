using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Purchasing.Persistence;
using ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.Queries;

internal sealed class PurchaseOrderQueries(
    PurchasingDbContext context,
    PurchaseOrderAuthorization authorization,
    PurchaseOrderEventReader reader
) : IPurchaseOrderQueries
{
    public async Task<GetPurchaseOrderResult> GetByCodeAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        string code,
        CancellationToken cancellationToken = default
    )
    {
        if (!await authorization.CanManageAsync(actorUserId, organizationId, cancellationToken))
            return new GetPurchaseOrderResult.PermissionDenied();
        try
        {
            code = PurchaseOrderDecider.Code(code, "code", 32);
        }
        catch (InvalidPurchaseOrderValueException exception)
        {
            return new GetPurchaseOrderResult.Invalid(exception.Message);
        }
        var row = await (
            from model in context.PurchaseOrderWriteModels.AsNoTracking()
            join stream in context.EventStreams.AsNoTracking() on model.StreamId equals stream.Id
            where model.OrganizationId == organizationId.Value && model.Code == code
            select new
            {
                Model = model,
                stream.UpdatedAt,
                stream.Version,
            }
        ).SingleOrDefaultAsync(cancellationToken);
        if (row is not null && row.Model.Version != row.Version)
            throw new PurchaseOrderIntegrityException(
                row.Model.StreamId,
                PurchaseOrderIntegrityFailure.WriteModelBehind
            );
        return row is null
            ? new GetPurchaseOrderResult.NotFound()
            : new GetPurchaseOrderResult.Found(
                row.Model.ToState().ToView(row.Model.StreamId, row.Model.Version, row.UpdatedAt)
            );
    }

    public Task<GetPurchaseOrderResult> GetLiveAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default
    ) =>
        ReconstructAsync(
            actorUserId,
            organizationId,
            purchaseOrderId,
            null,
            null,
            cancellationToken
        );

    public Task<GetPurchaseOrderResult> GetAtVersionAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        Guid purchaseOrderId,
        long version,
        CancellationToken cancellationToken = default
    ) =>
        ReconstructAsync(
            actorUserId,
            organizationId,
            purchaseOrderId,
            version,
            null,
            cancellationToken
        );

    public Task<GetPurchaseOrderResult> GetRecordedAtAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        Guid purchaseOrderId,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken = default
    ) =>
        ReconstructAsync(
            actorUserId,
            organizationId,
            purchaseOrderId,
            null,
            recordedAt,
            cancellationToken
        );

    private async Task<GetPurchaseOrderResult> ReconstructAsync(
        UserId userId,
        OrganizationId organizationId,
        Guid id,
        long? version,
        DateTimeOffset? recordedAt,
        CancellationToken cancellationToken
    )
    {
        if (!await authorization.CanManageAsync(userId, organizationId, cancellationToken))
            return new GetPurchaseOrderResult.PermissionDenied();
        if (version < 1)
            return new GetPurchaseOrderResult.Invalid("Version must be positive.");
        var result = await reader.ReadAsync(
            organizationId.Value,
            id,
            version,
            recordedAt,
            cancellationToken
        );
        return result is null
            ? new GetPurchaseOrderResult.NotFound()
            : new GetPurchaseOrderResult.Found(
                result.State.ToView(id, result.Version, result.RecordedAt)
            );
    }

    public async Task<ListPurchaseOrderSummariesResult> ListSummariesAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        int limit = 25,
        CancellationToken cancellationToken = default
    )
    {
        if (!await authorization.CanManageAsync(actorUserId, organizationId, cancellationToken))
            return new ListPurchaseOrderSummariesResult.PermissionDenied();
        if (limit is < 1 or > 100)
            return new ListPurchaseOrderSummariesResult.Invalid("Limit must be between 1 and 100.");
        var rows = await context
            .PurchaseOrderSummaries.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId.Value)
            .OrderBy(x => x.Code)
            .Take(limit)
            .Select(x => new PurchaseOrderSummaryView(
                x.StreamId,
                x.Code,
                x.Currency,
                x.IsIssued,
                x.LineCount,
                x.Total,
                x.Version
            ))
            .ToListAsync(cancellationToken);
        return new ListPurchaseOrderSummariesResult.Listed(rows);
    }
}
