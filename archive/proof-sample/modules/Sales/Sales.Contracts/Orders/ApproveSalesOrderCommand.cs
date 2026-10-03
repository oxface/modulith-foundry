using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Sales.Contracts;

public sealed record ApproveSalesOrderCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    long OrderNumber,
    long ExpectedVersion
);
