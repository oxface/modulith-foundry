namespace ModulithFoundry.Modules.Access.Contracts;

public interface IOrganizationQueries
{
    Task<IReadOnlyList<OrganizationMembership>> ListAccessibleToAsync(
        UserId userId,
        CancellationToken cancellationToken = default
    );

    Task<OrganizationAccessContext?> ResolveAccessAsync(
        UserId userId,
        string organizationSlug,
        CancellationToken cancellationToken = default
    );
}
