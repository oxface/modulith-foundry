namespace ModulithFoundry.Api.Modules.Access.Invitations;

internal abstract record InvitationAcceptanceNavigation
{
    private const string ResultPath = "/invitations/accept/result";
    private const string RecipientMismatchStatus = "recipient-mismatch";
    private const string UnavailableStatus = "invalid";

    private InvitationAcceptanceNavigation()
    {
    }

    internal abstract string ReturnUri { get; }

    internal static bool IsRecipientMismatch(string? status) =>
        string.Equals(status, RecipientMismatchStatus, StringComparison.Ordinal);

    internal sealed record Organization(string Slug) : InvitationAcceptanceNavigation
    {
        internal override string ReturnUri => $"/api/o/{Uri.EscapeDataString(Slug)}";
    }

    internal sealed record RecipientMismatch(string AcceptanceHandle) : InvitationAcceptanceNavigation
    {
        internal override string ReturnUri =>
            $"{ResultPath}?status={RecipientMismatchStatus}&acceptanceHandle={Uri.EscapeDataString(AcceptanceHandle)}";
    }

    internal sealed record Unavailable : InvitationAcceptanceNavigation
    {
        internal override string ReturnUri => $"{ResultPath}?status={UnavailableStatus}";
    }
}
