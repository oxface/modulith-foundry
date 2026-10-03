using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Purchasing.Contracts;

public sealed record CreatePurchaseOrderCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    string Code,
    string SupplierReference,
    string Currency
);
