namespace ModulithFoundry.Modules.Access.Contracts;

public sealed record OrganizationMembership(
    OrganizationId OrganizationId,
    string Name,
    string Slug,
    IReadOnlyList<string> RoleIds
);
