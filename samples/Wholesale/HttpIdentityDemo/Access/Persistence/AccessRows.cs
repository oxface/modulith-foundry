namespace ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Access.Persistence;

internal sealed class UserRow
{
    public required string Id { get; init; }
}

internal sealed class ExternalIdentityRow
{
    public required string Issuer { get; init; }
    public required string Subject { get; init; }
    public required string UserId { get; init; }
}

internal sealed class OrganizationRow
{
    public required string Id { get; init; }
    public required string Slug { get; init; }
}

internal enum MembershipStatus
{
    Active = 1,
    Suspended = 2,
    Removed = 3,
}

internal sealed class MembershipRow
{
    public Guid Id { get; init; }
    public required string OrganizationId { get; init; }
    public required string UserId { get; init; }
    public MembershipStatus Status { get; set; }
}
