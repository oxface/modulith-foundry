using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Organizations;
using ModulithFoundry.Modules.Access.Persistence;

namespace ModulithFoundry.Modules.Access.Invitations.AcceptInvitation;

internal sealed class AcceptInvitationHandler(
    AccessDbContext context,
    InvitationQueries invitationQueries,
    TimeProvider timeProvider)
{
    internal async Task<AcceptOrganizationInvitationResult> HandleAsync(
        AcceptOrganizationInvitationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Invitation? invitation = await invitationQueries.FindByIdAsync(
            command.InvitationId.Value,
            cancellationToken);
        if (invitation is null
            || string.IsNullOrWhiteSpace(command.Secret)
            || !InvitationSecret.Matches(command.Secret, invitation.SecretDigest))
        {
            return new AcceptOrganizationInvitationResult.Invalid();
        }

        if (!InvitationEmailAddress.TryCreate(
            command.AssuredEmail,
            out InvitationEmailAddress assuredEmail)
            || assuredEmail.Value != invitation.RecipientEmail)
        {
            return new AcceptOrganizationInvitationResult.RecipientMismatch();
        }

        DateTimeOffset acceptedAt = timeProvider.GetUtcNow();
        string[] roleIds = [.. invitation.RoleAssignments
            .Select(role => role.RoleId)
            .Order(StringComparer.Ordinal)];
        if (invitation.Status == InvitationStatus.Accepted
            && invitation.AcceptedByUserId == command.UserId.Value)
        {
            OrganizationMembership existingMembership = await ToMembershipAsync(
                invitation,
                roleIds,
                cancellationToken);
            return new AcceptOrganizationInvitationResult.AlreadyAccepted(existingMembership);
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            return new AcceptOrganizationInvitationResult.Consumed();
        }

        if (invitation.ExpiresAt <= acceptedAt)
        {
            return new AcceptOrganizationInvitationResult.Expired();
        }

        bool userExists = await context.Users.AnyAsync(
            user => user.Id == command.UserId.Value,
            cancellationToken);
        if (!userExists)
        {
            throw new InvalidOperationException($"Product user '{command.UserId.Value}' does not exist.");
        }

        OrganizationMembership acceptedMembership = await ToMembershipAsync(
            invitation,
            roleIds,
            cancellationToken);
        invitation.Accept(command.UserId.Value, acceptedAt);
        Membership membership = Membership.CreateFromInvitation(
            Guid.CreateVersion7(acceptedAt),
            invitation.OrganizationId,
            command.UserId.Value,
            roleIds,
            acceptedAt);
        context.Memberships.Add(membership);
        context.AuditEntries.Add(InvitationAuditEntries.Accepted(
            invitation,
            command.UserId.Value,
            roleIds,
            acceptedAt));
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            Invitation? accepted = await invitationQueries.FindByIdAsync(
                command.InvitationId.Value,
                cancellationToken);
            if (accepted is null || accepted.Status != InvitationStatus.Accepted)
            {
                throw;
            }

            if (accepted.AcceptedByUserId != command.UserId.Value)
            {
                return new AcceptOrganizationInvitationResult.Consumed();
            }

            return new AcceptOrganizationInvitationResult.AlreadyAccepted(acceptedMembership);
        }

        return new AcceptOrganizationInvitationResult.Accepted(acceptedMembership);
    }

    private async Task<OrganizationMembership> ToMembershipAsync(
        Invitation invitation,
        IReadOnlyList<string> roleIds,
        CancellationToken cancellationToken)
    {
        Organization organization = await context.Organizations
            .AsNoTracking()
            .IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
            .SingleAsync(
                candidate => candidate.Id == invitation.OrganizationId,
                cancellationToken);
        return new OrganizationMembership(
            new OrganizationId(organization.Id),
            organization.Name,
            organization.Slug.Value,
            roleIds);
    }
}
