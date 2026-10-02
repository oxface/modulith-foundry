using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Purchasing.Audit;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Purchasing.Persistence;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders;

internal sealed class PurchaseOrderAuthorization(
    IOrganizationContextAccessor accessor,
    IOrganizationAuthorization authorization,
    PurchasingDbContext context,
    TimeProvider timeProvider
)
{
    internal Task<bool> CanManageAsync(
        UserId userId,
        OrganizationId organizationId,
        CancellationToken cancellationToken
    ) =>
        accessor.OrganizationContext is { } current
        && current.UserId == userId
        && current.OrganizationId == organizationId
            ? authorization.HasPermissionAsync(
                userId,
                organizationId,
                PurchasingPermissionIds.PurchaseOrdersManage,
                cancellationToken
            )
            : Task.FromResult(false);

    internal async Task<bool> AuthorizeWriteAsync(
        UserId userId,
        OrganizationId organizationId,
        string deniedAction,
        Guid subjectId,
        CancellationToken cancellationToken
    )
    {
        if (
            accessor.OrganizationContext is not { } current
            || current.UserId != userId
            || current.OrganizationId != organizationId
        )
            return false;
        if (await CanManageAsync(userId, organizationId, cancellationToken))
            return true;
        context.AuditEntries.Add(
            PurchasingAuditEntry.RecordHuman(
                organizationId.Value,
                userId.Value,
                deniedAction,
                subjectId,
                new { },
                timeProvider.GetUtcNow(),
                PurchasingAuditOutcomes.Denied,
                PurchasingAuditReasons.PermissionDenied
            )
        );
        await context.SaveChangesAsync(cancellationToken);
        return false;
    }
}
