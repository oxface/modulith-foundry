namespace ModulithFoundry.Modules.Access.Contracts;

public interface IOrganizationInvitations
{
    Task<CreateOrganizationInvitationResult> CreateInvitationAsync(
        CreateOrganizationInvitationCommand command,
        CancellationToken cancellationToken = default);

    Task<ResendOrganizationInvitationResult> ResendInvitationAsync(
        ResendOrganizationInvitationCommand command,
        CancellationToken cancellationToken = default);
}
