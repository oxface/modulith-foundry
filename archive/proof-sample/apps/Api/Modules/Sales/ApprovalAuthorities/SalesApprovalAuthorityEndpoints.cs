using System.Diagnostics;
using System.Text.Json;
using ModulithFoundry.Api.Authentication;
using ModulithFoundry.Api.Modules.Access.Middleware;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.Api.Modules.Sales.ApprovalAuthorities;

internal static class SalesApprovalAuthorityEndpoints
{
    internal static RouteGroupBuilder MapSalesApprovalAuthorityEndpoints(
        this RouteGroupBuilder sales
    )
    {
        RouteGroupBuilder authorities = sales.MapGroup("/approval-authorities/{membershipId:guid}");
        authorities.MapPut("", SetAsync).RequireBffAntiforgery();
        authorities.MapGet("", GetAsync);
        authorities.MapPost("/revoke", RevokeAsync).RequireBffAntiforgery();
        return sales;
    }

    private static async Task<IResult> SetAsync(
        Guid membershipId,
        SetRequest request,
        IOrganizationContextAccessor contextAccessor,
        ISalesApprovalAuthorityAdministration administration,
        CancellationToken cancellationToken
    )
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        SetSalesApprovalAuthorityResult result = await administration.SetAsync(
            new(
                context.UserId,
                context.OrganizationId,
                new(membershipId),
                request.MaximumAmount,
                request.Currency,
                request.ExpectedVersion
            ),
            cancellationToken
        );
        return result switch
        {
            SetSalesApprovalAuthorityResult.Saved saved => TypedResults.Ok(
                ToResponse(saved.Authority)
            ),
            SetSalesApprovalAuthorityResult.Invalid invalid => InvalidAuthority(
                invalid.Field,
                invalid.Detail
            ),
            SetSalesApprovalAuthorityResult.MembershipUnavailable => Results.NotFound(),
            SetSalesApprovalAuthorityResult.VersionConflict => VersionConflict(),
            SetSalesApprovalAuthorityResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    private static async Task<IResult> GetAsync(
        Guid membershipId,
        IOrganizationContextAccessor contextAccessor,
        ISalesApprovalAuthorityAdministration administration,
        CancellationToken cancellationToken
    )
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        GetSalesApprovalAuthorityResult result = await administration.GetAsync(
            context.UserId,
            context.OrganizationId,
            new(membershipId),
            cancellationToken
        );
        return result switch
        {
            GetSalesApprovalAuthorityResult.Found found => TypedResults.Ok(
                ToResponse(found.Authority)
            ),
            GetSalesApprovalAuthorityResult.NotFound => Results.NotFound(),
            GetSalesApprovalAuthorityResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    private static async Task<IResult> RevokeAsync(
        Guid membershipId,
        RevokeRequest request,
        IOrganizationContextAccessor contextAccessor,
        ISalesApprovalAuthorityAdministration administration,
        CancellationToken cancellationToken
    )
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        RevokeSalesApprovalAuthorityResult result = await administration.RevokeAsync(
            new(context.UserId, context.OrganizationId, new(membershipId), request.ExpectedVersion),
            cancellationToken
        );
        return result switch
        {
            RevokeSalesApprovalAuthorityResult.Revoked revoked => TypedResults.Ok(
                ToResponse(revoked.Authority)
            ),
            RevokeSalesApprovalAuthorityResult.Unchanged unchanged => TypedResults.Ok(
                ToResponse(unchanged.Authority)
            ),
            RevokeSalesApprovalAuthorityResult.InvalidExpectedVersion => InvalidAuthority(
                "expectedVersion",
                "Expected version must be positive."
            ),
            RevokeSalesApprovalAuthorityResult.NotFound => Results.NotFound(),
            RevokeSalesApprovalAuthorityResult.VersionConflict => VersionConflict(),
            RevokeSalesApprovalAuthorityResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    private static IResult InvalidAuthority(string field, string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid approval authority",
            detail: detail,
            extensions: new Dictionary<string, object?>
            {
                ["field"] = JsonNamingPolicy.CamelCase.ConvertName(field),
            }
        );

    private static IResult VersionConflict() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Approval authority version conflict",
            detail: "The authority has changed. Reload it before retrying."
        );

    private static AuthorityResponse ToResponse(SalesApprovalAuthorityView view) =>
        new(
            view.MembershipId.Value,
            view.MaximumAmount,
            view.Currency,
            view.IsEnabled,
            view.Version
        );

    private sealed record SetRequest(decimal MaximumAmount, string Currency, long ExpectedVersion);

    private sealed record RevokeRequest(long ExpectedVersion);

    private sealed record AuthorityResponse(
        Guid MembershipId,
        decimal MaximumAmount,
        string Currency,
        bool IsEnabled,
        long Version
    );
}
