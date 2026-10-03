using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Access.Invitations;

internal static class InvitationMappings
{
    internal static OrganizationInvitation ToContract(this Invitation invitation) =>
        new(
            new InvitationId(invitation.Id),
            new OrganizationId(invitation.OrganizationId),
            invitation.RecipientEmail,
            [
                .. invitation
                    .RoleAssignments.Select(role => role.RoleId)
                    .Order(StringComparer.Ordinal),
            ],
            invitation.ExpiresAt,
            invitation.CreatedAt
        );
}
