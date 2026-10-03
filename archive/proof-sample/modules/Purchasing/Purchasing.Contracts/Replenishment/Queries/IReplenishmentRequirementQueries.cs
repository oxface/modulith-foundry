using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Purchasing.Contracts;

public interface IReplenishmentRequirementQueries
{
    Task<GetReplenishmentRequirementResult> GetByNumberAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        long number,
        CancellationToken cancellationToken = default
    );
    Task<ListReplenishmentRequirementsResult> ListAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        long afterNumber = 0,
        int limit = 25,
        CancellationToken cancellationToken = default
    );
}
