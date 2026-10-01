namespace ModulithFoundry.Modules.Access.Contracts;

public interface IOrganizationAuthorization
{
    Task<bool> HasPermissionAsync(
        UserId userId,
        OrganizationId organizationId,
        string permissionId,
        CancellationToken cancellationToken = default
    );
}
