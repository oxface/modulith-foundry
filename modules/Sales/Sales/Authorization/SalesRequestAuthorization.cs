using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Sales.Authorization;

internal sealed class SalesRequestAuthorization(IOrganizationContextAccessor contextAccessor, IOrganizationAuthorization authorization)
{
    internal bool MatchesContext(UserId userId, OrganizationId organizationId) =>
        contextAccessor.OrganizationContext is { } current && current.UserId == userId && current.OrganizationId == organizationId;

    internal Task<bool> HasPermissionAsync(UserId userId, OrganizationId organizationId, string permission, CancellationToken cancellationToken) =>
        MatchesContext(userId, organizationId)
            ? authorization.HasPermissionAsync(userId, organizationId, permission, cancellationToken)
            : Task.FromResult(false);
}
