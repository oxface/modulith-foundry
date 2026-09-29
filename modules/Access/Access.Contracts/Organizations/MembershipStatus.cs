namespace ModulithFoundry.Modules.Access.Contracts;

public enum MembershipStatus
{
    Active = 1,
    Suspended = 2,
    Removed = 3,
}

public static class MembershipStatusValues
{
    public const string Active = "active";
    public const string Suspended = "suspended";
    public const string Removed = "removed";

    public static string ToValue(MembershipStatus status) =>
        status switch
        {
            MembershipStatus.Active => Active,
            MembershipStatus.Suspended => Suspended,
            MembershipStatus.Removed => Removed,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unknown membership status."),
        };

    public static MembershipStatus FromValue(string value) =>
        value switch
        {
            Active => MembershipStatus.Active,
            Suspended => MembershipStatus.Suspended,
            Removed => MembershipStatus.Removed,
            _ => throw new InvalidOperationException(
                $"Unknown membership status value '{value}'."),
        };
}
