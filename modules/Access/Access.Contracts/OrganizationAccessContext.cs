namespace ModulithFoundry.Modules.Access.Contracts;

public sealed record OrganizationAccessContext(
    UserId UserId,
    OrganizationId OrganizationId,
    MembershipId MembershipId,
    string OrganizationName,
    string OrganizationSlug,
    IReadOnlyList<string> RoleIds);
