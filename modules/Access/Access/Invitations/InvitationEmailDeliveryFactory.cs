using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace ModulithFoundry.Modules.Access.Invitations;

internal sealed class InvitationEmailDeliveryFactory(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<InvitationOptions> options)
{
    private const string DeliveryPayloadPurpose =
        "ModulithFoundry.Access.InvitationEmailDelivery.v1";

    private readonly InvitationOptions _options = options.Value;
    private readonly IDataProtector _payloadProtector =
        dataProtectionProvider.CreateProtector(DeliveryPayloadPurpose);

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
        string protectedPayload = _payloadProtector.Protect(JsonSerializer.Serialize(
            new InvitationEmailPayload(
                invitation.RecipientEmail,
                organizationName,
                acceptUrl,
                invitation.ExpiresAt)));
        return InvitationEmailDelivery.Create(
            Guid.CreateVersion7(createdAt),
            invitation,
            protectedPayload,
            createdAt);
    }
}
