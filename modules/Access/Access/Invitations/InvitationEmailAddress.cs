using System.Net.Mail;

namespace ModulithFoundry.Modules.Access.Invitations;

internal readonly record struct InvitationEmailAddress
{
    private InvitationEmailAddress(string value)
    {
        Value = value;
    }

    internal string Value { get; }

    internal static bool TryCreate(string? proposed, out InvitationEmailAddress email)
    {
        string candidate = proposed?.Trim() ?? string.Empty;
        if (candidate.Length > 320
            || !MailAddress.TryCreate(candidate, out MailAddress? parsed)
            || !string.Equals(candidate, parsed.Address, StringComparison.OrdinalIgnoreCase))
        {
            email = default;
            return false;
        }

        email = new InvitationEmailAddress(parsed.Address.ToLowerInvariant());
        return true;
    }

    public override string ToString() => Value;
}
