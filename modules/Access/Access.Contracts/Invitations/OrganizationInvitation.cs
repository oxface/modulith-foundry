namespace ModulithFoundry.Modules.Access.Contracts;

public sealed record OrganizationInvitation(
    InvitationId InvitationId,
    OrganizationId OrganizationId,
    string RecipientEmail,
    IReadOnlyList<string> RoleIds,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);
