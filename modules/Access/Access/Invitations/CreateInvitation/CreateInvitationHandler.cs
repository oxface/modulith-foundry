using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Organizations;
using ModulithFoundry.Modules.Access.Organizations.Queries;
using ModulithFoundry.Modules.Access.Persistence;
using Npgsql;

namespace ModulithFoundry.Modules.Access.Invitations.CreateInvitation;

internal sealed class CreateInvitationHandler(
    AccessDbContext context,
    OrganizationMembershipQueries membershipQueries,
    InvitationQueries invitationQueries,
    InvitationEmailDeliveryFactory deliveryFactory,
    SystemRoleCatalog roleCatalog,
    TimeProvider timeProvider)
{
    internal async Task<CreateOrganizationInvitationResult> HandleAsync(
        CreateOrganizationInvitationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!InvitationEmailAddress.TryCreate(command.RecipientEmail, out InvitationEmailAddress email))
        {
            return new CreateOrganizationInvitationResult.InvalidEmail(
                "An invitation recipient must be a valid email address.");
        }

        string[] roleIds = [.. command.RoleIds
            .Where(static roleId => !string.IsNullOrWhiteSpace(roleId))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        string[] invalidRoleIds = [.. roleIds.Where(roleId => !roleCatalog.Contains(roleId))];
        if (roleIds.Length == 0 || invalidRoleIds.Length > 0)
        {
            return new CreateOrganizationInvitationResult.InvalidRoles(invalidRoleIds);
        }

        if (!await membershipQueries.HasPermissionAsync(
            command.ActorUserId.Value,
            command.OrganizationId.Value,
            AccessPermissionIds.MembersManage,
            cancellationToken))
        {
            return new CreateOrganizationInvitationResult.PermissionDenied();
        }

        if (await membershipQueries.HasActiveMembershipForEmailAsync(
            command.OrganizationId.Value,
            email.Value,
            cancellationToken))
        {
            return new CreateOrganizationInvitationResult.AlreadyMember();
        }

        Invitation? pending = await invitationQueries.FindPendingAsync(
            command.OrganizationId.Value,
            email.Value,
            cancellationToken);
        if (pending is not null)
        {
            return new CreateOrganizationInvitationResult.InvitationAlreadyPending(
                pending.ToContract());
        }

        DateTimeOffset createdAt = timeProvider.GetUtcNow();
        string secret = InvitationSecret.Generate();
        Invitation invitation = Invitation.Create(
            Guid.CreateVersion7(createdAt),
            command.OrganizationId.Value,
            email,
            InvitationSecret.Digest(secret),
            roleIds,
            createdAt,
            deliveryFactory.InvitationLifetime);
        string organizationName = await context.Organizations
            .AsNoTracking()
            .IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
            .Where(organization => organization.Id == command.OrganizationId.Value)
            .Select(organization => organization.Name)
            .SingleAsync(cancellationToken);

        context.Invitations.Add(invitation);
        context.InvitationEmailDeliveries.Add(
            deliveryFactory.Create(invitation, secret, organizationName, createdAt));
        context.AuditEntries.Add(InvitationAuditEntries.Created(
            invitation,
            command.ActorUserId.Value,
            roleIds,
            createdAt));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsPendingInvitationConflict(exception))
        {
            context.ChangeTracker.Clear();
            Invitation existing = await invitationQueries.FindPendingAsync(
                command.OrganizationId.Value,
                email.Value,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "The pending invitation conflict could not be reloaded.",
                    exception);
            return new CreateOrganizationInvitationResult.InvitationAlreadyPending(
                existing.ToContract());
        }

        return new CreateOrganizationInvitationResult.Created(invitation.ToContract());
    }

    private static bool IsPendingInvitationConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_invitations_organization_pending_email",
        };
}
