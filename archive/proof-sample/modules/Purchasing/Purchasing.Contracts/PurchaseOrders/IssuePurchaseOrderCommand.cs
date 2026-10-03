using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Purchasing.Contracts;

public sealed record IssuePurchaseOrderCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    Guid PurchaseOrderId,
    long ExpectedVersion
);
