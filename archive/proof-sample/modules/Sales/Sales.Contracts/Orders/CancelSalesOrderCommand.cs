using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Sales.Contracts;

public sealed record CancelSalesOrderCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    long OrderNumber,
    long ExpectedVersion,
    string Reason
);
