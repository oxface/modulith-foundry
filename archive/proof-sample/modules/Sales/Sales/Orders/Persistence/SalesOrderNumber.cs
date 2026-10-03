using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Sales.Orders.Persistence;

internal sealed class SalesOrderNumber : IOrganizationOwned
{
    public Guid OrganizationId { get; private set; }
    internal long LastNumber { get; private set; }
}
