using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Sales.Contracts;

// ExpectedVersion is zero for creation and the current version for replacement.
public sealed record SetSalesApprovalAuthorityCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    MembershipId MembershipId,
    decimal MaximumAmount,
    string Currency,
    long ExpectedVersion
);
