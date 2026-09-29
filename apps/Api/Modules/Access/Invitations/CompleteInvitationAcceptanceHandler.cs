using ModulithFoundry.Api.Authentication;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Api.Modules.Access.Invitations;

internal sealed class CompleteInvitationAcceptanceHandler(
    RedisPendingInvitationAcceptanceStore pendingAcceptances,
    IOrganizationInvitations invitations)
{
    internal async Task<InvitationAcceptanceNavigation> HandleAsync(
        string acceptanceHandle,
        CurrentUser currentUser,
        string? verifiedProviderEmail,
        CancellationToken cancellationToken)
    {
        PendingInvitationAcceptance? pendingAcceptance = await pendingAcceptances.RetrieveAsync(
            acceptanceHandle,
            cancellationToken);
        if (pendingAcceptance is null)
        {
            return new InvitationAcceptanceNavigation.Unavailable();
        }

        if (verifiedProviderEmail is null)
        {
            return new InvitationAcceptanceNavigation.RecipientMismatch(acceptanceHandle);
        }

        AcceptOrganizationInvitationResult result = await invitations.AcceptInvitationAsync(
            new AcceptOrganizationInvitationCommand(
                currentUser.UserId,
                new InvitationId(pendingAcceptance.InvitationId),
                pendingAcceptance.Secret,
                verifiedProviderEmail),
            cancellationToken);

        return result switch
        {
            AcceptOrganizationInvitationResult.Accepted accepted =>
                await CompleteAsync(acceptanceHandle, accepted.Membership, cancellationToken),
            AcceptOrganizationInvitationResult.AlreadyAccepted accepted =>
                await CompleteAsync(acceptanceHandle, accepted.Membership, cancellationToken),
            AcceptOrganizationInvitationResult.RecipientMismatch =>
                new InvitationAcceptanceNavigation.RecipientMismatch(acceptanceHandle),
            AcceptOrganizationInvitationResult.Invalid =>
                await CompleteTerminalAsync(acceptanceHandle, cancellationToken),
            AcceptOrganizationInvitationResult.Expired =>
                await CompleteTerminalAsync(acceptanceHandle, cancellationToken),
            AcceptOrganizationInvitationResult.Consumed =>
                await CompleteTerminalAsync(acceptanceHandle, cancellationToken),
            _ => throw new InvalidOperationException("Unknown invitation acceptance result."),
        };
    }

    private async Task<InvitationAcceptanceNavigation> CompleteAsync(
        string acceptanceHandle,
        OrganizationMembership membership,
        CancellationToken cancellationToken)
    {
        await pendingAcceptances.RemoveAsync(acceptanceHandle, cancellationToken);
        return new InvitationAcceptanceNavigation.Organization(membership.Slug);
    }

    private async Task<InvitationAcceptanceNavigation> CompleteTerminalAsync(
        string acceptanceHandle,
        CancellationToken cancellationToken)
    {
        await pendingAcceptances.RemoveAsync(acceptanceHandle, cancellationToken);
        return new InvitationAcceptanceNavigation.Unavailable();
    }
}
