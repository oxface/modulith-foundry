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
        memberships.MapPost("/{membershipId:guid}/suspend", SuspendAsync)
            .RequireBffAntiforgery();
        memberships.MapPost("/{membershipId:guid}/reactivate", ReactivateAsync)
            .RequireBffAntiforgery();
        memberships.MapPost("/{membershipId:guid}/remove", RemoveAsync)
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
            ReplaceMembershipRolesResult.InvalidMembershipStatus invalid => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Membership roles cannot be changed",
                detail: $"A {MembershipStatusValues.ToValue(invalid.Status)} membership cannot receive role changes."),
            ReplaceMembershipRolesResult.NotFound => Results.NotFound(),
            ReplaceMembershipRolesResult.PermissionDenied => Results.Forbid(),
            ReplaceMembershipRolesResult.LastAdministrator => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Last organization administrator",
                detail: "An active organization must retain at least one administrator."),
            _ => throw new UnreachableException(),
        };
    }

    private static Task<IResult> SuspendAsync(
        Guid membershipId,
        IOrganizationContextAccessor contextAccessor,
        IOrganizationMembershipAdministration administration,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(
            membershipId,
            MembershipStatus.Suspended,
            contextAccessor,
            administration,
            cancellationToken);

    private static Task<IResult> ReactivateAsync(
        Guid membershipId,
        IOrganizationContextAccessor contextAccessor,
        IOrganizationMembershipAdministration administration,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(
            membershipId,
            MembershipStatus.Active,
            contextAccessor,
            administration,
            cancellationToken);

    private static Task<IResult> RemoveAsync(
        Guid membershipId,
        IOrganizationContextAccessor contextAccessor,
        IOrganizationMembershipAdministration administration,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(
            membershipId,
            MembershipStatus.Removed,
            contextAccessor,
            administration,
            cancellationToken);

    private static async Task<IResult> ChangeStatusAsync(
        Guid membershipId,
        MembershipStatus status,
        IOrganizationContextAccessor contextAccessor,
        IOrganizationMembershipAdministration administration,
        CancellationToken cancellationToken)
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        ChangeMembershipStatusResult result = await administration.ChangeStatusAsync(
            new ChangeMembershipStatusCommand(
                context.UserId,
                context.OrganizationId,
                new MembershipId(membershipId),
                status),
            cancellationToken);

        return result switch
        {
            ChangeMembershipStatusResult.Changed changed => TypedResults.Ok(
                new MembershipStatusResponse(
                    changed.MembershipId.Value,
                    MembershipStatusValues.ToValue(changed.Status))),
            ChangeMembershipStatusResult.Unchanged unchanged => TypedResults.Ok(
                new MembershipStatusResponse(
                    unchanged.MembershipId.Value,
                    MembershipStatusValues.ToValue(unchanged.Status))),
            ChangeMembershipStatusResult.NotFound => Results.NotFound(),
            ChangeMembershipStatusResult.PermissionDenied => Results.Forbid(),
            ChangeMembershipStatusResult.LastAdministrator => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Last organization administrator",
                detail: "An active organization must retain at least one administrator."),
            ChangeMembershipStatusResult.InvalidTransition invalid => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Invalid membership status transition",
                detail: $"A {MembershipStatusValues.ToValue(invalid.CurrentStatus)} membership cannot become {MembershipStatusValues.ToValue(invalid.RequestedStatus)}."),
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
                MembershipStatusValues.ToValue(member.Status),
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
        string Status,
        IReadOnlyList<string> RoleIds);

    private sealed record SystemRoleResponse(
        string Id,
        string DisplayName,
        IReadOnlyList<string> PermissionIds);

    private sealed record SystemPermissionResponse(string Id, string DisplayName);

    private sealed record MembershipRolesResponse(
        Guid MembershipId,
        IReadOnlyList<string> RoleIds);

    private sealed record MembershipStatusResponse(Guid MembershipId, string Status);

}
