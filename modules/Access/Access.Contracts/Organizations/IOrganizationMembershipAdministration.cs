namespace ModulithFoundry.Modules.Access.Contracts;

public interface IOrganizationMembershipAdministration
{
    Task<ReplaceMembershipRolesResult> ReplaceRolesAsync(
        ReplaceMembershipRolesCommand command,
        CancellationToken cancellationToken = default);
}
