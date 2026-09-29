using System.Diagnostics;
using ModulithFoundry.Api.Authentication;
using ModulithFoundry.Api.Modules.Access.Middleware;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Api.Modules.Access.Memberships;

internal static class MembershipEndpoints
{
    internal static IEndpointRouteBuilder MapMembershipEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder memberships = endpoints.MapGroup("/access/members");
        memberships.MapGet("", ListMembersAsync);
        memberships.MapPut("/{membershipId:guid}/roles", ReplaceRolesAsync)
            .RequireBffAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> ListMembersAsync(
        IOrganizationContextAccessor contextAccessor,
        IOrganizationMembershipQueries queries,
        CancellationToken cancellationToken)
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        ListOrganizationMembersResult result = await queries.ListForAdministrationAsync(
            new ListOrganizationMembersQuery(context.UserId, context.OrganizationId),
            cancellationToken);

        return result switch
        {
            ListOrganizationMembersResult.Listed listed =>
                TypedResults.Ok(ToResponse(listed.View)),
            ListOrganizationMembersResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    private static async Task<IResult> ReplaceRolesAsync(
        Guid membershipId,
        ReplaceMembershipRolesRequest request,
        IOrganizationContextAccessor contextAccessor,
        IOrganizationMembershipAdministration administration,
        CancellationToken cancellationToken)
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        ReplaceMembershipRolesResult result = await administration.ReplaceRolesAsync(
            new ReplaceMembershipRolesCommand(
                context.UserId,
                context.OrganizationId,
                new MembershipId(membershipId),
                request.RoleIds),
            cancellationToken);

        return result switch
        {
            ReplaceMembershipRolesResult.Updated updated => TypedResults.Ok(
                new MembershipRolesResponse(updated.MembershipId.Value, updated.RoleIds)),
            ReplaceMembershipRolesResult.InvalidRoles invalid => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid membership roles",
                detail: invalid.RoleIds.Count == 0
                    ? "At least one system role is required."
                    : $"Unknown system roles: {string.Join(", ", invalid.RoleIds)}."),
            ReplaceMembershipRolesResult.NotFound => Results.NotFound(),
            ReplaceMembershipRolesResult.PermissionDenied => Results.Forbid(),
            ReplaceMembershipRolesResult.LastAdministrator => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Last organization administrator",
                detail: "An active organization must retain at least one administrator."),
            _ => throw new UnreachableException(),
        };
    }

    private static MembershipAdministrationResponse ToResponse(
        OrganizationMembershipAdministration administration) =>
        new(
            [.. administration.Members.Select(member => new OrganizationMemberResponse(
                member.MembershipId.Value,
                member.UserId.Value,
                member.Email,
                member.DisplayName,
                member.RoleIds))],
            [.. administration.SystemRoles.Select(role => new SystemRoleResponse(
                role.Id,
                role.DisplayName,
                role.PermissionIds))],
            [.. administration.SystemPermissions.Select(permission =>
                new SystemPermissionResponse(permission.Id, permission.DisplayName))]);

    private sealed record MembershipAdministrationResponse(
        IReadOnlyList<OrganizationMemberResponse> Members,
        IReadOnlyList<SystemRoleResponse> SystemRoles,
        IReadOnlyList<SystemPermissionResponse> SystemPermissions);

    private sealed record OrganizationMemberResponse(
        Guid MembershipId,
        Guid UserId,
        string? Email,
        string? DisplayName,
        IReadOnlyList<string> RoleIds);

    private sealed record SystemRoleResponse(
        string Id,
        string DisplayName,
        IReadOnlyList<string> PermissionIds);

    private sealed record SystemPermissionResponse(string Id, string DisplayName);

    private sealed record MembershipRolesResponse(
        Guid MembershipId,
        IReadOnlyList<string> RoleIds);
}
