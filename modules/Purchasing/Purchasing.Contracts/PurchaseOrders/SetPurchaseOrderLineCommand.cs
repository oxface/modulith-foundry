using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Purchasing.Contracts;

public sealed record SetPurchaseOrderLineCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    Guid PurchaseOrderId,
    long ExpectedVersion,
    string ItemCode,
    decimal Quantity,
    decimal UnitPrice
);
