namespace ModulithFoundry.Modules.Access.Email;

internal sealed class SmtpOptions
{
    internal const string SectionName = "Email:Smtp";

    public string Host { get; set; } = null!;

    public int Port { get; set; }

    public SmtpSecurity Security { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = null!;

    public string FromName { get; set; } = "Modulith Foundry";
}
