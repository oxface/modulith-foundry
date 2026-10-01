using System.Diagnostics;

namespace ModulithFoundry.Modules.Access.Organizations;

internal static class OrganizationAuditEntries
{
    internal static AccessAuditEntry Created(
        Organization organization,
        Guid actorUserId,
        DateTimeOffset occurredAt
    ) =>
        AccessAuditEntry.Create(
            Guid.CreateVersion7(occurredAt),
            organization.Id,
            actorUserId,
            "organization.created",
            "organization",
            organization.Id,
            schemaVersion: 1,
            new { name = organization.Name, slug = organization.Slug.Value },
            Activity.Current?.RootId,
            Activity.Current?.TraceId.ToHexString(),
            occurredAt
        );
}
