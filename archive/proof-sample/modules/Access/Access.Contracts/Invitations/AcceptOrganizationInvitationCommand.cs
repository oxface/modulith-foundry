namespace ModulithFoundry.Modules.Access.Contracts;

public sealed record AcceptOrganizationInvitationCommand(
    UserId UserId,
    InvitationId InvitationId,
    string Secret,
    string VerifiedProviderEmail
);
