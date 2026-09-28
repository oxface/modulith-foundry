using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Invitations.CreateInvitation;
using ModulithFoundry.Modules.Access.Invitations.ResendInvitation;

namespace ModulithFoundry.Modules.Access.Invitations;

internal sealed class OrganizationInvitations(
    CreateInvitationHandler createInvitation,
    ResendInvitationHandler resendInvitation) : IOrganizationInvitations
{
    public Task<CreateOrganizationInvitationResult> CreateInvitationAsync(
        CreateOrganizationInvitationCommand command,
        CancellationToken cancellationToken = default) =>
        createInvitation.HandleAsync(command, cancellationToken);

    public Task<ResendOrganizationInvitationResult> ResendInvitationAsync(
        ResendOrganizationInvitationCommand command,
        CancellationToken cancellationToken = default) =>
        resendInvitation.HandleAsync(command, cancellationToken);
}
