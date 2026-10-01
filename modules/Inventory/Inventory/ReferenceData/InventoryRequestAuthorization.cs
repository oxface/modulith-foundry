using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.ReferenceData;

internal sealed class InventoryRequestAuthorization(
    IOrganizationContextAccessor organizationContextAccessor,
    IOrganizationAuthorization authorization
)
{
    internal async Task<bool> HasPermissionAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        string permissionId,
        CancellationToken cancellationToken
    )
    {
        return MatchesContext(actorUserId, organizationId)
            && await authorization.HasPermissionAsync(
                actorUserId,
                organizationId,
                permissionId,
                cancellationToken
            );
    }

    internal bool MatchesContext(UserId actorUserId, OrganizationId organizationId) =>
        organizationContextAccessor.OrganizationContext is { } current
        && current.UserId == actorUserId
        && current.OrganizationId == organizationId;
}
