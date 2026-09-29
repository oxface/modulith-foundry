using System.Diagnostics;

namespace ModulithFoundry.Modules.Access.Organizations;

internal static class MembershipAuditEntries
{
    internal static AccessAuditEntry RolesReplaced(
        Membership membership,
        Guid actorUserId,
        IReadOnlyCollection<string> previousRoleIds,
        IReadOnlyCollection<string> currentRoleIds,
        DateTimeOffset occurredAt) =>
        AccessAuditEntry.Create(
            Guid.CreateVersion7(occurredAt),
            membership.OrganizationId,
            actorUserId,
            "membership.roles-replaced",
            "membership",
            membership.Id,
            schemaVersion: 1,
            new
            {
                previousRoleIds,
                currentRoleIds,
            },
            Activity.Current?.RootId,
            Activity.Current?.TraceId.ToHexString(),
            occurredAt);

    internal static AccessAuditEntry RoleReplacementDenied(
        Guid organizationId,
        Guid membershipId,
        Guid actorUserId,
        string reasonCode,
        IReadOnlyCollection<string> requestedRoleIds,
        DateTimeOffset occurredAt) =>
        AccessAuditEntry.CreateDenied(
            Guid.CreateVersion7(occurredAt),
            organizationId,
            actorUserId,
            "membership.roles-replacement-denied",
            "membership",
            membershipId,
            reasonCode,
            schemaVersion: 1,
            new
            {
                requestedRoleIds,
            },
            Activity.Current?.RootId,
            Activity.Current?.TraceId.ToHexString(),
            occurredAt);
}
