using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Persistence;

namespace ModulithFoundry.Modules.Access.Invitations;

internal sealed class InvitationQueries(AccessDbContext context)
{
    internal Task<Invitation?> FindPendingAsync(
        Guid organizationId,
        string recipientEmail,
        CancellationToken cancellationToken) =>
        Pending(organizationId)
            .SingleOrDefaultAsync(
                invitation => invitation.RecipientEmail == recipientEmail,
                cancellationToken);

    internal Task<Invitation?> FindPendingAsync(
        Guid organizationId,
        Guid invitationId,
        CancellationToken cancellationToken) =>
        Pending(organizationId)
            .SingleOrDefaultAsync(
                invitation => invitation.Id == invitationId,
                cancellationToken);

    internal Task<InvitationEmailDelivery[]> ListPendingDeliveriesAsync(
        Invitation invitation,
        CancellationToken cancellationToken) =>
        context.InvitationEmailDeliveries
            .IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
            .Where(delivery => delivery.InvitationId == invitation.Id)
            .Where(delivery => delivery.OrganizationId == invitation.OrganizationId)
            .Where(delivery => delivery.SentAt == null && delivery.SupersededAt == null)
            .ToArrayAsync(cancellationToken);

    private IQueryable<Invitation> Pending(Guid organizationId) =>
        context.Invitations
            .IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
            .Include(invitation => invitation.RoleAssignments)
            .Where(invitation => invitation.OrganizationId == organizationId)
            .Where(invitation => invitation.Status == InvitationStatus.Pending);
}
