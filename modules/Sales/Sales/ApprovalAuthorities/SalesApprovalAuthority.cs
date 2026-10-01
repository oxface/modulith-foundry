using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.Modules.Sales.ApprovalAuthorities;

internal sealed class SalesApprovalAuthority : IOrganizationOwned
{
    private SalesApprovalAuthority()
    {
        Currency = null!;
    }

    internal Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal Guid MembershipId { get; private set; }
    internal decimal MaximumAmount { get; private set; }
    internal string Currency { get; private set; }
    internal bool IsEnabled { get; private set; }
    internal long Version { get; private set; }

    internal static SalesApprovalAuthority Create(
        Guid organizationId,
        Guid membershipId,
        ApprovalLimit limit
    )
    {
        var authority = new SalesApprovalAuthority
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            MembershipId = membershipId,
        };
        authority.Set(limit);
        return authority;
    }

    internal void Set(ApprovalLimit limit)
    {
        MaximumAmount = limit.MaximumAmount;
        Currency = limit.Currency;
        IsEnabled = true;
        Version = checked(Version + 1);
    }

    internal bool TryRevoke()
    {
        if (!IsEnabled)
            return false;
        IsEnabled = false;
        Version = checked(Version + 1);
        return true;
    }

    internal SalesApprovalAuthorityView ToView() =>
        new(new(MembershipId), MaximumAmount, Currency, IsEnabled, Version);
}
