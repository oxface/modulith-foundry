using System.Diagnostics;
using ModulithFoundry.Api.Modules.Access.Middleware;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;

namespace ModulithFoundry.Api.Modules.Purchasing.Requirements;

internal static class ReplenishmentRequirementEndpoints
{
    internal static RouteGroupBuilder MapReplenishmentRequirementEndpoints(
        this RouteGroupBuilder purchasing
    )
    {
        var requirements = purchasing.MapGroup("/requirements");
        requirements.MapGet("/{number:long}", GetAsync);
        requirements.MapGet("", ListAsync);
        return purchasing;
    }

    private static async Task<IResult> GetAsync(
        long number,
        IOrganizationContextAccessor accessor,
        IReplenishmentRequirementQueries queries,
        CancellationToken cancellationToken
    )
    {
        var context = accessor.GetRequiredOrganizationContext();
        var result = await queries.GetByNumberAsync(
            context.UserId,
            context.OrganizationId,
            number,
            cancellationToken
        );
        return result switch
        {
            GetReplenishmentRequirementResult.Found found => TypedResults.Ok(
                ToResponse(found.Requirement)
            ),
            GetReplenishmentRequirementResult.NotFound => Results.NotFound(),
            GetReplenishmentRequirementResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    private static async Task<IResult> ListAsync(
        IOrganizationContextAccessor accessor,
        IReplenishmentRequirementQueries queries,
        CancellationToken cancellationToken,
        long afterNumber = 0,
        int limit = 25
    )
    {
        var context = accessor.GetRequiredOrganizationContext();
        var result = await queries.ListAsync(
            context.UserId,
            context.OrganizationId,
            afterNumber,
            limit,
            cancellationToken
        );
        return result switch
        {
            ListReplenishmentRequirementsResult.Listed listed => TypedResults.Ok(
                listed.Requirements.Select(ToResponse).ToArray()
            ),
            ListReplenishmentRequirementsResult.PermissionDenied => Results.Forbid(),
            ListReplenishmentRequirementsResult.Invalid invalid => Results.Problem(
                statusCode: 400,
                title: "Invalid requirement query",
                detail: invalid.Detail
            ),
            _ => throw new UnreachableException(),
        };
    }

    private static ReplenishmentRequirementResponse ToResponse(
        ReplenishmentRequirementView requirement
    ) =>
        new(
            requirement.RequirementId,
            requirement.Number,
            requirement.StockItemId,
            requirement.Sku,
            requirement.Description,
            requirement.Quantity,
            requirement.BaseUnitCode,
            requirement.CreatedAt
        );

    private sealed record ReplenishmentRequirementResponse(
        Guid RequirementId,
        long Number,
        Guid StockItemId,
        string Sku,
        string Description,
        decimal Quantity,
        string BaseUnitCode,
        DateTimeOffset CreatedAt
    );
}
