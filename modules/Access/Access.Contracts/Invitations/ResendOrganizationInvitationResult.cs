namespace ModulithFoundry.Modules.Access.Contracts;

public abstract record ResendOrganizationInvitationResult
{
    private ResendOrganizationInvitationResult()
    {
    }

    public sealed record Resent(OrganizationInvitation Invitation)
        : ResendOrganizationInvitationResult;

    public sealed record NotFound : ResendOrganizationInvitationResult;

    public sealed record PermissionDenied : ResendOrganizationInvitationResult;

    public sealed record Conflict : ResendOrganizationInvitationResult;
}
