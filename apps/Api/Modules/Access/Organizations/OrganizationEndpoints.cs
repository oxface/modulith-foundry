using System.Diagnostics;
using System.Security.Claims;
using ModulithFoundry.Api.Authentication;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Api.Modules.Access.Organizations;

internal static class OrganizationEndpoints
{
    internal static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder organizations = endpoints.MapGroup("/api/organizations")
            .RequireAuthorization();

        organizations.MapPost("/", CreateOrganizationAsync)
            .RequireBffAntiforgery();
        organizations.MapGet("/", ListOrganizationsAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        ClaimsPrincipal principal,
        IOrganizationCreation organizationCreation,
        CancellationToken cancellationToken)
    {
        CreateOrganizationResult result = await organizationCreation.CreateOrganizationAsync(
            new CreateOrganizationCommand(
                GetUserId(principal),
                request.Name,
                request.Slug),
            cancellationToken);

        return result switch
        {
            CreateOrganizationResult.Created created => Results.Json(
                ToResponse(created.Organization),
                statusCode: StatusCodes.Status201Created),
            CreateOrganizationResult.InvalidName invalid => InvalidOrganization(invalid.Detail),
            CreateOrganizationResult.InvalidSlug invalid => InvalidOrganization(invalid.Detail),
            CreateOrganizationResult.SlugUnavailable unavailable => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Organization slug unavailable",
                detail: $"Organization slug '{unavailable.Slug}' is unavailable."),
            _ => throw new UnreachableException(),
        };
    }

    private static async Task<IReadOnlyList<OrganizationResponse>> ListOrganizationsAsync(
        ClaimsPrincipal principal,
        IOrganizationQueries queries,
        CancellationToken cancellationToken) =>
        [.. (await queries.ListAccessibleToAsync(
            GetUserId(principal),
            cancellationToken)).Select(ToResponse)];

    private static UserId GetUserId(ClaimsPrincipal principal) =>
        new(Guid.Parse(principal.GetRequiredClaimValue(ProductClaims.UserId)));

    private static OrganizationResponse ToResponse(OrganizationMembership organization) =>
        new(
            organization.OrganizationId.Value,
            organization.Name,
            organization.Slug,
            organization.RoleIds);

    private static IResult InvalidOrganization(string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid organization",
            detail: detail);

    private sealed record CreateOrganizationRequest(string Name, string Slug);

    private sealed record OrganizationResponse(
        Guid OrganizationId,
        string Name,
        string Slug,
        IReadOnlyList<string> RoleIds);
}
