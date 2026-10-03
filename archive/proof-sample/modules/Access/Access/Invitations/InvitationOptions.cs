namespace ModulithFoundry.Modules.Access.Invitations;

internal sealed class InvitationOptions
{
    internal const string SectionName = "Invitations";

    public Uri? PublicApplicationUrl { get; set; }

    public int LifetimeHours { get; set; } = 168;
}
