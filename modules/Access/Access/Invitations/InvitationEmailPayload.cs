namespace ModulithFoundry.Modules.Access.Invitations;

internal sealed record InvitationEmailPayload(
    string RecipientEmail,
    string OrganizationName,
    Uri AcceptUrl,
    DateTimeOffset ExpiresAt
);
