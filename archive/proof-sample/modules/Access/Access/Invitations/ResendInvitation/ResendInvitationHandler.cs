using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Organizations;
using ModulithFoundry.Modules.Access.Organizations.Queries;
using ModulithFoundry.Modules.Access.Persistence;

namespace ModulithFoundry.Modules.Access.Invitations.ResendInvitation;

internal sealed class ResendInvitationHandler(
    AccessDbContext context,
    OrganizationMembershipQueries membershipQueries,
    InvitationQueries invitationQueries,
    InvitationEmailDeliveryFactory deliveryFactory,
    TimeProvider timeProvider
)
{
    internal async Task<ResendOrganizationInvitationResult> HandleAsync(
        ResendOrganizationInvitationCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        if (
            !await membershipQueries.HasPermissionAsync(
                command.ActorUserId.Value,
                command.OrganizationId.Value,
                AccessPermissionIds.MembersManage,
                cancellationToken
            )
        )
        {
            return new ResendOrganizationInvitationResult.PermissionDenied();
        }

        Invitation? invitation = await invitationQueries.FindPendingAsync(
            command.OrganizationId.Value,
            command.InvitationId.Value,
            cancellationToken
        );
        if (invitation is null)
        {
            return new ResendOrganizationInvitationResult.NotFound();
        }

        DateTimeOffset resentAt = timeProvider.GetUtcNow();
        string secret = InvitationSecret.Generate();
        invitation.Resend(
            InvitationSecret.Digest(secret),
            resentAt,
            deliveryFactory.InvitationLifetime
        );
        foreach (
            InvitationEmailDelivery delivery in await invitationQueries.ListPendingDeliveriesAsync(
                invitation,
                cancellationToken
            )
        )
        {
            delivery.Supersede(resentAt);
        }

        string organizationName = await context
            .Organizations.AsNoTracking()
            .IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
            .Where(organization => organization.Id == command.OrganizationId.Value)
            .Select(organization => organization.Name)
            .SingleAsync(cancellationToken);
        context.InvitationEmailDeliveries.Add(
            deliveryFactory.Create(invitation, secret, organizationName, resentAt)
        );
        context.AuditEntries.Add(
            InvitationAuditEntries.Resent(invitation, command.ActorUserId.Value, resentAt)
        );

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ResendOrganizationInvitationResult.Conflict();
        }

        return new ResendOrganizationInvitationResult.Resent(invitation.ToContract());
    }
}
