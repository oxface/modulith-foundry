namespace ModulithFoundry.Modules.Access.Contracts;

public sealed record ExternalIdentity
{
    private ExternalIdentity(
        string issuer,
        string subject,
        string? email,
        string? displayName)
    {
        Issuer = issuer;
        Subject = subject;
        Email = email;
        DisplayName = displayName;
    }

    public string Issuer { get; }

    public string Subject { get; }

    public string? Email { get; }

    public string? DisplayName { get; }

    public static ExternalIdentity Create(
        string issuer,
        string subject,
        string? email,
        string? displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(issuer.Length, 512);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(subject.Length, 255);
        string? normalizedEmail = NormalizeOptional(email);
        string? normalizedDisplayName = NormalizeOptional(displayName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(normalizedEmail?.Length ?? 0, 320);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(normalizedDisplayName?.Length ?? 0, 200);

        return new ExternalIdentity(issuer, subject, normalizedEmail, normalizedDisplayName);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
