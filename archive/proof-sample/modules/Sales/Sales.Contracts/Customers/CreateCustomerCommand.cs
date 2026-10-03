using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Sales.Contracts;

public sealed record CreateCustomerCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    string Code,
    string Name
);
