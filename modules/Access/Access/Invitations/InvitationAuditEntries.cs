using System.Diagnostics;
using ModulithFoundry.Modules.Access.Organizations;

namespace ModulithFoundry.Modules.Access.Invitations;

internal static class InvitationAuditEntries
{
    internal static AccessAuditEntry Created(
        Invitation invitation,
        Guid actorUserId,
        IReadOnlyCollection<string> roleIds,
        DateTimeOffset occurredAt) =>
        AccessAuditEntry.Create(
            Guid.CreateVersion7(occurredAt),
            invitation.OrganizationId,
            actorUserId,
            "invitation.created",
            "invitation",
            invitation.Id,
            schemaVersion: 1,
            new
            {
                recipientEmail = invitation.RecipientEmail,
                roleIds,
                invitation.ExpiresAt,
            },
            Activity.Current?.RootId,
            Activity.Current?.TraceId.ToHexString(),
            occurredAt);

    internal static AccessAuditEntry Resent(
        Invitation invitation,
        Guid actorUserId,
        DateTimeOffset occurredAt) =>
        AccessAuditEntry.Create(
            Guid.CreateVersion7(occurredAt),
            invitation.OrganizationId,
            actorUserId,
            "invitation.resent",
            "invitation",
            invitation.Id,
            schemaVersion: 1,
            new
            {
                recipientEmail = invitation.RecipientEmail,
                roleIds = invitation.RoleAssignments
                    .Select(role => role.RoleId)
                    .Order(StringComparer.Ordinal),
                invitation.ExpiresAt,
            },
            Activity.Current?.RootId,
            Activity.Current?.TraceId.ToHexString(),
            occurredAt);
}
