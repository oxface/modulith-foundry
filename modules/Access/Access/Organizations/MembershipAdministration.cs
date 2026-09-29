using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Organizations.ChangeMembershipStatus;
using ModulithFoundry.Modules.Access.Organizations.ReplaceMembershipRoles;

namespace ModulithFoundry.Modules.Access.Organizations;

internal sealed class MembershipAdministration(
    ChangeMembershipStatusHandler changeStatus,
    ReplaceMembershipRolesHandler replaceRoles) : IOrganizationMembershipAdministration
{
    public Task<ChangeMembershipStatusResult> ChangeStatusAsync(
        ChangeMembershipStatusCommand command,
        CancellationToken cancellationToken = default) =>
        changeStatus.HandleAsync(command, cancellationToken);

    public Task<ReplaceMembershipRolesResult> ReplaceRolesAsync(
        ReplaceMembershipRolesCommand command,
        CancellationToken cancellationToken = default) =>
        replaceRoles.HandleAsync(command, cancellationToken);
}
