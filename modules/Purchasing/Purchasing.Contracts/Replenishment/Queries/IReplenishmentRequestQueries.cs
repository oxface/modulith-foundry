using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Purchasing.Contracts;

// Trusted workflow/operator diagnostic capability, not an HTTP authorization adapter.
public interface IReplenishmentRequestQueries
{
    Task<ReplenishmentRequestView?> GetAsync(
        OrganizationId organizationId,
        Guid operationId,
        CancellationToken cancellationToken = default
    );
}
