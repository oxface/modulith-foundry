namespace ModulithFoundry.Modules.Access.Contracts;

public interface IOrganizationInvitationOperations
{
    Task<CreateOrganizationInvitationResult> CreateInvitationAsync(
        CreateOrganizationInvitationCommand command,
        CancellationToken cancellationToken = default);

    Task<ResendOrganizationInvitationResult> ResendInvitationAsync(
        ResendOrganizationInvitationCommand command,
        CancellationToken cancellationToken = default);

    Task<AcceptOrganizationInvitationResult> AcceptInvitationAsync(
        AcceptOrganizationInvitationCommand command,
        CancellationToken cancellationToken = default);
}
