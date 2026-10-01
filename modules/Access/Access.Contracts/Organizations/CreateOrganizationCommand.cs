namespace ModulithFoundry.Modules.Access.Contracts;

public sealed record CreateOrganizationCommand(
    UserId ActorUserId,
    string Name,
    string ProposedSlug
);
