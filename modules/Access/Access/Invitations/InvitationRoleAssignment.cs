using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Access.Invitations;

internal sealed class InvitationRoleAssignment : IOrganizationOwned
{
    private InvitationRoleAssignment()
    {
        RoleId = null!;
    }

    private InvitationRoleAssignment(Guid invitationId, Guid organizationId, string roleId)
    {
        InvitationId = invitationId;
        OrganizationId = organizationId;
        RoleId = roleId;
    }

    internal Guid InvitationId { get; private set; }

    public Guid OrganizationId { get; private set; }

    internal string RoleId { get; private set; }

    internal static InvitationRoleAssignment Create(
        Guid invitationId,
        Guid organizationId,
        string roleId
    ) => new(invitationId, organizationId, roleId);
}
