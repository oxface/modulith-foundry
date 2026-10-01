using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Sales.Contracts;

public sealed record SalesApprovalAuthorityView(
    MembershipId MembershipId,
    decimal MaximumAmount,
    string Currency,
    bool IsEnabled,
    long Version
);
