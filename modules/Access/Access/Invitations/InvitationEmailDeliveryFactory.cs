using Microsoft.Extensions.Options;
using ModulithFoundry.Modules.Access.Invitations.Email;

namespace ModulithFoundry.Modules.Access.Invitations;

internal sealed class InvitationEmailDeliveryFactory(
    InvitationEmailPayloadCodec payloadCodec,
    IOptions<InvitationOptions> options)
{
    private readonly InvitationOptions _options = options.Value;

    internal TimeSpan InvitationLifetime => TimeSpan.FromHours(_options.LifetimeHours);

    internal InvitationEmailDelivery Create(
        Invitation invitation,
        string secret,
        string organizationName,
        DateTimeOffset createdAt)
    {
        Uri acceptUrl = new(
            _options.PublicApplicationUrl
                ?? throw new InvalidOperationException("Public application URL is not configured."),
            $"/invitations/accept?invitationId={invitation.Id:D}&code={Uri.EscapeDataString(secret)}");
        string protectedPayload = payloadCodec.Protect(
            new InvitationEmailPayload(
                invitation.RecipientEmail,
                organizationName,
                acceptUrl,
                invitation.ExpiresAt));
        return InvitationEmailDelivery.Create(
            Guid.CreateVersion7(createdAt),
            invitation,
            protectedPayload,
            createdAt);
    }
}
