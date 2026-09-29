namespace ModulithFoundry.Modules.Access.Contracts;

public sealed record OrganizationMember(
    MembershipId MembershipId,
    UserId UserId,
    string? Email,
    string? DisplayName,
    IReadOnlyList<string> RoleIds);
