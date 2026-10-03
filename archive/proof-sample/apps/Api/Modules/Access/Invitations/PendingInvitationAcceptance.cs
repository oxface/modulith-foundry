namespace ModulithFoundry.Api.Modules.Access.Invitations;

internal sealed record PendingInvitationAcceptance(Guid InvitationId, string Secret);
