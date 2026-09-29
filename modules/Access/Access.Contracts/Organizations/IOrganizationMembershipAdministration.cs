namespace ModulithFoundry.Modules.Access.Contracts;

public interface IOrganizationMembershipAdministration
{
    Task<ChangeMembershipStatusResult> ChangeStatusAsync(
        ChangeMembershipStatusCommand command,
        CancellationToken cancellationToken = default);

    Task<ReplaceMembershipRolesResult> ReplaceRolesAsync(
        ReplaceMembershipRolesCommand command,
        CancellationToken cancellationToken = default);
}
