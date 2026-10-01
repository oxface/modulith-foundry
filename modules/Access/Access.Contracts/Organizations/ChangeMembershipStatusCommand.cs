namespace ModulithFoundry.Modules.Access.Contracts;

public sealed record ChangeMembershipStatusCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    MembershipId MembershipId,
    MembershipStatus Status
);
