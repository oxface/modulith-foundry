namespace ModulithFoundry.Modules.Access.Contracts;

public interface IOrganizationAuthorizationGuard
{
    // Null means denied. A live guard keeps this exact active tenure and permission stable.
    // Hold it only through a short local database operation; dispose after commit/rollback.
    // Do not use it across external calls, durable workflows, or service boundaries.
    Task<IAsyncDisposable?> TryAcquireAsync(
        UserId userId,
        OrganizationId organizationId,
        MembershipId membershipId,
        string permissionId,
        CancellationToken cancellationToken = default
    );
}
