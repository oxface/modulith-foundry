namespace ModulithFoundry.Modules.Access.Contracts;

public abstract record ReplaceMembershipRolesResult
{
    private ReplaceMembershipRolesResult()
    {
    }

    public sealed record Updated(
        MembershipId MembershipId,
        IReadOnlyList<string> RoleIds) : ReplaceMembershipRolesResult;

    public sealed record InvalidRoles(IReadOnlyList<string> RoleIds)
        : ReplaceMembershipRolesResult;

    public sealed record InvalidMembershipStatus(MembershipStatus Status)
        : ReplaceMembershipRolesResult;

    public sealed record NotFound : ReplaceMembershipRolesResult;

    public sealed record PermissionDenied : ReplaceMembershipRolesResult;

    public sealed record LastAdministrator : ReplaceMembershipRolesResult;
}
