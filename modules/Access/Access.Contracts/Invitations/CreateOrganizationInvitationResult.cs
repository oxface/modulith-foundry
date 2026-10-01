namespace ModulithFoundry.Modules.Access.Contracts;

public abstract record CreateOrganizationInvitationResult
{
    private CreateOrganizationInvitationResult() { }

    public sealed record Created(OrganizationInvitation Invitation)
        : CreateOrganizationInvitationResult;

    public sealed record InvalidEmail(string Detail) : CreateOrganizationInvitationResult;

    public sealed record InvalidRoles(IReadOnlyList<string> RoleIds)
        : CreateOrganizationInvitationResult;

    public sealed record AlreadyMember : CreateOrganizationInvitationResult;

    public sealed record InvitationAlreadyPending(OrganizationInvitation Invitation)
        : CreateOrganizationInvitationResult;

    public sealed record PermissionDenied : CreateOrganizationInvitationResult;
}
