namespace ModulithFoundry.Modules.Access.Contracts;

public sealed record CreateOrganizationInvitationCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    string RecipientEmail,
    IReadOnlyCollection<string> RoleIds);
