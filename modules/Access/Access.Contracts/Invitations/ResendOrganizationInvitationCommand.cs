namespace ModulithFoundry.Modules.Access.Contracts;

public sealed record ResendOrganizationInvitationCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    InvitationId InvitationId
);
