using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Purchasing.Contracts;

public interface IPurchaseOrderQueries
{
    Task<GetPurchaseOrderResult> GetByCodeAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        string code,
        CancellationToken cancellationToken = default
    );
    Task<GetPurchaseOrderResult> GetLiveAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default
    );
    Task<GetPurchaseOrderResult> GetAtVersionAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        Guid purchaseOrderId,
        long version,
        CancellationToken cancellationToken = default
    );
    Task<GetPurchaseOrderResult> GetRecordedAtAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        Guid purchaseOrderId,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken = default
    );
    Task<ListPurchaseOrderSummariesResult> ListSummariesAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        int limit = 25,
        CancellationToken cancellationToken = default
    );
}
