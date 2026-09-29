using ModulithFoundry.Api.Authentication;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Api.Modules.Access.Invitations;

internal sealed class CompleteInvitationAcceptanceHandler(
    RedisPendingInvitationAcceptanceStore pendingAcceptances,
    IOrganizationInvitations invitations)
{
    internal async Task<string> HandleAsync(
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
            return ResultUri("invalid");
        }

        if (verifiedProviderEmail is null)
        {
            return ResultUri("recipient-mismatch", acceptanceHandle);
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
                ResultUri("recipient-mismatch", acceptanceHandle),
            AcceptOrganizationInvitationResult.Invalid =>
                await CompleteTerminalAsync(acceptanceHandle, cancellationToken),
            AcceptOrganizationInvitationResult.Expired =>
                await CompleteTerminalAsync(acceptanceHandle, cancellationToken),
            AcceptOrganizationInvitationResult.Consumed =>
                await CompleteTerminalAsync(acceptanceHandle, cancellationToken),
            _ => throw new InvalidOperationException("Unknown invitation acceptance result."),
        };
    }

    private async Task<string> CompleteAsync(
        string acceptanceHandle,
        OrganizationMembership membership,
        CancellationToken cancellationToken)
    {
        await pendingAcceptances.RemoveAsync(acceptanceHandle, cancellationToken);
        return $"/api/o/{Uri.EscapeDataString(membership.Slug)}";
    }

    private async Task<string> CompleteTerminalAsync(
        string acceptanceHandle,
        CancellationToken cancellationToken)
    {
        await pendingAcceptances.RemoveAsync(acceptanceHandle, cancellationToken);
        return ResultUri("invalid");
    }

    private static string ResultUri(string status, string? acceptanceHandle = null) =>
        acceptanceHandle is null
            ? $"/invitations/accept/result?status={Uri.EscapeDataString(status)}"
            : $"/invitations/accept/result?status={Uri.EscapeDataString(status)}&acceptanceHandle={Uri.EscapeDataString(acceptanceHandle)}";
}
