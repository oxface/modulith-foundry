using System.Diagnostics;
using ModulithFoundry.Api.Authentication;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Api.Modules.Access.Invitations;

internal static class InvitationEndpoints
{
    internal static IEndpointRouteBuilder MapInvitationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder invitations = endpoints.MapGroup("/invitations");
        invitations.MapPost("", CreateInvitationAsync)
            .RequireBffAntiforgery();
        invitations.MapPost("/{invitationId:guid}/resend", ResendInvitationAsync)
            .RequireBffAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> CreateInvitationAsync(
        CreateInvitationRequest request,
        IOrganizationContextAccessor contextAccessor,
        IOrganizationInvitations invitations,
        CancellationToken cancellationToken)
    {
        OrganizationAccessContext context = GetOrganizationContext(contextAccessor);
        CreateOrganizationInvitationResult result = await invitations.CreateInvitationAsync(
            new CreateOrganizationInvitationCommand(
                context.UserId,
                context.OrganizationId,
                request.RecipientEmail,
                request.RoleIds),
            cancellationToken);

        return result switch
        {
            CreateOrganizationInvitationResult.Created created => Results.Json(
                ToResponse(created.Invitation),
                statusCode: StatusCodes.Status201Created),
            CreateOrganizationInvitationResult.InvalidEmail invalid => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid invitation email",
                detail: invalid.Detail),
            CreateOrganizationInvitationResult.InvalidRoles invalid => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid invitation roles",
                detail: invalid.RoleIds.Count == 0
                    ? "At least one system role is required."
                    : $"Unknown system roles: {string.Join(", ", invalid.RoleIds)}."),
            CreateOrganizationInvitationResult.AlreadyMember => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Recipient is already a member"),
            CreateOrganizationInvitationResult.InvitationAlreadyPending pending => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Invitation already pending",
                detail: $"Invitation '{pending.Invitation.InvitationId.Value}' is already pending."),
            CreateOrganizationInvitationResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    private static async Task<IResult> ResendInvitationAsync(
        Guid invitationId,
        IOrganizationContextAccessor contextAccessor,
        IOrganizationInvitations invitations,
        CancellationToken cancellationToken)
    {
        OrganizationAccessContext context = GetOrganizationContext(contextAccessor);
        ResendOrganizationInvitationResult result = await invitations.ResendInvitationAsync(
            new ResendOrganizationInvitationCommand(
                context.UserId,
                context.OrganizationId,
                new InvitationId(invitationId)),
            cancellationToken);

        return result switch
        {
            ResendOrganizationInvitationResult.Resent resent =>
                TypedResults.Ok(ToResponse(resent.Invitation)),
            ResendOrganizationInvitationResult.NotFound => Results.NotFound(),
            ResendOrganizationInvitationResult.PermissionDenied => Results.Forbid(),
            ResendOrganizationInvitationResult.Conflict => Results.Conflict(),
            _ => throw new UnreachableException(),
        };
    }

    private static OrganizationAccessContext GetOrganizationContext(
        IOrganizationContextAccessor contextAccessor) =>
        contextAccessor.OrganizationContext
            ?? throw new InvalidOperationException("Organization context is not resolved.");

    private static InvitationResponse ToResponse(OrganizationInvitation invitation) =>
        new(
            invitation.InvitationId.Value,
            invitation.RecipientEmail,
            invitation.RoleIds,
            invitation.ExpiresAt,
            invitation.CreatedAt);

    private sealed record InvitationResponse(
        Guid InvitationId,
        string RecipientEmail,
        IReadOnlyList<string> RoleIds,
        DateTimeOffset ExpiresAt,
        DateTimeOffset CreatedAt);
}
