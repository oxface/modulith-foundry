using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Purchasing.Persistence;

namespace ModulithFoundry.Modules.Purchasing.Replenishment.Requests;

internal sealed class ReplenishmentRequestQueries(PurchasingDbContext context)
    : IReplenishmentRequestQueries
{
    public async Task<ReplenishmentRequestView?> GetAsync(
        OrganizationId organizationId,
        Guid operationId,
        CancellationToken cancellationToken = default
    )
    {
        ReplenishmentRequest? request = await context
            .ReplenishmentRequests.AsNoTracking()
            .IgnoreQueryFilters([PurchasingDbContext.OrganizationScopeFilter])
            .SingleOrDefaultAsync(
                x => x.OrganizationId == organizationId.Value && x.OperationId == operationId,
                cancellationToken
            );
        return request is null
            ? null
            : new(request.OperationId, request.Status, request.RequirementId, request.ReasonCode);
    }
}
