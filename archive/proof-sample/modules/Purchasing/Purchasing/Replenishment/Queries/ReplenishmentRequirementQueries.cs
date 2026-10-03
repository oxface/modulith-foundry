using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Purchasing.Persistence;

namespace ModulithFoundry.Modules.Purchasing.Replenishment.Queries;

internal sealed class ReplenishmentRequirementQueries(
    PurchasingDbContext context,
    IOrganizationContextAccessor accessor,
    IOrganizationAuthorization authorization
) : IReplenishmentRequirementQueries
{
    private Task<bool> CanReadAsync(
        UserId userId,
        OrganizationId organizationId,
        CancellationToken cancellationToken
    ) =>
        accessor.OrganizationContext is { } current
        && current.UserId == userId
        && current.OrganizationId == organizationId
            ? authorization.HasPermissionAsync(
                userId,
                organizationId,
                PurchasingPermissionIds.RequirementsView,
                cancellationToken
            )
            : Task.FromResult(false);

    public async Task<GetReplenishmentRequirementResult> GetByNumberAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        long number,
        CancellationToken cancellationToken = default
    )
    {
        if (!await CanReadAsync(actorUserId, organizationId, cancellationToken))
            return new GetReplenishmentRequirementResult.PermissionDenied();
        var requirement = await context
            .Requirements.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId.Value && x.Number == number)
            .Select(x => new ReplenishmentRequirementView(
                x.Id,
                x.Number,
                x.StockItemId,
                x.Sku,
                x.Description,
                x.Quantity,
                x.BaseUnitCode,
                x.ReferenceRevision,
                x.CreatedAt
            ))
            .SingleOrDefaultAsync(cancellationToken);
        return requirement is null
            ? new GetReplenishmentRequirementResult.NotFound()
            : new GetReplenishmentRequirementResult.Found(requirement);
    }

    public async Task<ListReplenishmentRequirementsResult> ListAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        long afterNumber = 0,
        int limit = 25,
        CancellationToken cancellationToken = default
    )
    {
        if (!await CanReadAsync(actorUserId, organizationId, cancellationToken))
            return new ListReplenishmentRequirementsResult.PermissionDenied();
        if (afterNumber < 0 || limit is < 1 or > 100)
            return new ListReplenishmentRequirementsResult.Invalid(
                "The cursor must be nonnegative and limit must be between 1 and 100."
            );
        var requirements = await context
            .Requirements.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId.Value && x.Number > afterNumber)
            .OrderBy(x => x.Number)
            .Take(limit)
            .Select(x => new ReplenishmentRequirementView(
                x.Id,
                x.Number,
                x.StockItemId,
                x.Sku,
                x.Description,
                x.Quantity,
                x.BaseUnitCode,
                x.ReferenceRevision,
                x.CreatedAt
            ))
            .ToListAsync(cancellationToken);
        return new ListReplenishmentRequirementsResult.Listed(requirements);
    }
}
