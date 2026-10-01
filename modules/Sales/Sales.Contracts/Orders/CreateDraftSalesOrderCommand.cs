using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Sales.Contracts;

public sealed record CreateDraftSalesOrderCommand(
    UserId ActorUserId, OrganizationId OrganizationId, string CustomerCode, string Currency, IReadOnlyList<DraftSalesOrderLine> Lines);
