namespace ModulithFoundry.Modules.Access.Contracts;

public abstract record ChangeMembershipStatusResult
{
    private ChangeMembershipStatusResult() { }

    public sealed record Changed(MembershipId MembershipId, MembershipStatus Status)
        : ChangeMembershipStatusResult;

    public sealed record Unchanged(MembershipId MembershipId, MembershipStatus Status)
        : ChangeMembershipStatusResult;

    public sealed record InvalidTransition(
        MembershipStatus CurrentStatus,
        MembershipStatus RequestedStatus
    ) : ChangeMembershipStatusResult;

    public sealed record NotFound : ChangeMembershipStatusResult;

    public sealed record PermissionDenied : ChangeMembershipStatusResult;

    public sealed record LastAdministrator : ChangeMembershipStatusResult;
}
