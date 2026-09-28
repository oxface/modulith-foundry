using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Invitations.AcceptInvitation;
using ModulithFoundry.Modules.Access.Invitations.CreateInvitation;
using ModulithFoundry.Modules.Access.Invitations.ResendInvitation;

namespace ModulithFoundry.Modules.Access.Invitations;

internal sealed class OrganizationInvitations(
    CreateInvitationHandler createInvitation,
    ResendInvitationHandler resendInvitation,
    AcceptInvitationHandler acceptInvitation) : IOrganizationInvitations
{
    public Task<CreateOrganizationInvitationResult> CreateInvitationAsync(
        CreateOrganizationInvitationCommand command,
        CancellationToken cancellationToken = default) =>
        createInvitation.HandleAsync(command, cancellationToken);

    public Task<ResendOrganizationInvitationResult> ResendInvitationAsync(
        ResendOrganizationInvitationCommand command,
        CancellationToken cancellationToken = default) =>
        resendInvitation.HandleAsync(command, cancellationToken);

    public Task<AcceptOrganizationInvitationResult> AcceptInvitationAsync(
        AcceptOrganizationInvitationCommand command,
        CancellationToken cancellationToken = default) =>
        acceptInvitation.HandleAsync(command, cancellationToken);
}
