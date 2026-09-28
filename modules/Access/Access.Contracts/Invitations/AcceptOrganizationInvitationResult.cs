namespace ModulithFoundry.Modules.Access.Contracts;

public abstract record AcceptOrganizationInvitationResult
{
    private AcceptOrganizationInvitationResult()
    {
    }

    public sealed record Accepted(
        OrganizationMembership Membership) : AcceptOrganizationInvitationResult;

    public sealed record AlreadyAccepted(
        OrganizationMembership Membership) : AcceptOrganizationInvitationResult;

    public sealed record Invalid : AcceptOrganizationInvitationResult;

    public sealed record RecipientMismatch : AcceptOrganizationInvitationResult;

    public sealed record Expired : AcceptOrganizationInvitationResult;

    public sealed record Consumed : AcceptOrganizationInvitationResult;
}
