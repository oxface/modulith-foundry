using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace ModulithFoundry.Modules.Access.Invitations.Email;

internal sealed class InvitationEmailPayloadCodec(IDataProtectionProvider dataProtectionProvider)
{
    private const string PayloadPurpose =
        "ModulithFoundry.Access.InvitationEmailDelivery.v1";

    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector(PayloadPurpose);

    internal string Protect(InvitationEmailPayload payload) =>
        _protector.Protect(JsonSerializer.Serialize(payload));

    internal InvitationEmailPayload Unprotect(string protectedPayload) =>
        JsonSerializer.Deserialize<InvitationEmailPayload>(
            _protector.Unprotect(protectedPayload))
        ?? throw new InvalidOperationException("Invitation email payload is empty.");
}
