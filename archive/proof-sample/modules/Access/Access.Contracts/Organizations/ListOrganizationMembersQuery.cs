namespace ModulithFoundry.Modules.Access.Contracts;

public sealed record ListOrganizationMembersQuery(
    UserId ActorUserId,
    OrganizationId OrganizationId
);
