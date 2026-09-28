using System.Text.Encodings.Web;
using ModulithFoundry.Modules.Access.Email;

using ModulithFoundry.Modules.Access.ExtensionPoints;

namespace ModulithFoundry.Modules.Access.Invitations.Email;

internal static class InvitationEmailRenderer
{
    internal static EmailMessage Render(InvitationEmailPayload payload)
    {
        string organizationName = HtmlEncoder.Default.Encode(payload.OrganizationName);
        string acceptUrl = HtmlEncoder.Default.Encode(payload.AcceptUrl.AbsoluteUri);
        string expiresAt = payload.ExpiresAt.ToString("u", System.Globalization.CultureInfo.InvariantCulture);

        return new EmailMessage(
            payload.RecipientEmail,
            $"Invitation to {payload.OrganizationName}",
            $"""
            You have been invited to join {payload.OrganizationName}.

            Accept the invitation: {payload.AcceptUrl.AbsoluteUri}

            This invitation expires at {expiresAt}.
            """,
            $"""
            <!doctype html>
            <html lang="en">
              <body>
                <h1>Invitation to {organizationName}</h1>
                <p>You have been invited to join {organizationName}.</p>
                <p><a href="{acceptUrl}">Accept invitation</a></p>
                <p>This invitation expires at {expiresAt}.</p>
              </body>
            </html>
            """);
    }
}
