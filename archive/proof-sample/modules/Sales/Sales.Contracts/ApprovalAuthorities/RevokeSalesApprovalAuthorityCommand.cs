using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Sales.Contracts;

public sealed record RevokeSalesApprovalAuthorityCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    MembershipId MembershipId,
    long ExpectedVersion
);
