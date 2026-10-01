using System.Text.Json;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.ReferenceData;

internal sealed class InventoryAuditEntry : IOrganizationOwned
{
    private InventoryAuditEntry()
    {
        Action = null!;
        SubjectType = null!;
        Outcome = null!;
        SourceModule = null!;
    }

    private InventoryAuditEntry(
        Guid id,
        Guid organizationId,
        Guid actorUserId,
        string action,
        string subjectType,
        Guid subjectId,
        string outcome,
        string? reasonCode,
        JsonElement details,
        DateTimeOffset occurredAt
    )
    {
        Id = id;
        OrganizationId = organizationId;
        ActorUserId = actorUserId;
        Action = action;
        SubjectType = subjectType;
        SubjectId = subjectId;
        Outcome = outcome;
        ReasonCode = reasonCode;
        SourceModule = "inventory";
        SchemaVersion = 1;
        Details = details;
        OccurredAt = occurredAt;
    }

    internal Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    internal Guid ActorUserId { get; private set; }

    internal string Action { get; private set; }

    internal string SubjectType { get; private set; }

    internal Guid SubjectId { get; private set; }

    internal string Outcome { get; private set; }

    internal string? ReasonCode { get; private set; }

    internal string SourceModule { get; private set; }

    internal short SchemaVersion { get; private set; }

    internal JsonElement Details { get; private set; }

    internal DateTimeOffset OccurredAt { get; private set; }

    internal static InventoryAuditEntry Succeeded(
        Guid organizationId,
        Guid actorUserId,
        string action,
        string subjectType,
        Guid subjectId,
        object details,
        DateTimeOffset occurredAt
    ) =>
        new(
            Guid.CreateVersion7(occurredAt),
            organizationId,
            actorUserId,
            action,
            subjectType,
            subjectId,
            InventoryAuditOutcomes.Succeeded,
            reasonCode: null,
            JsonSerializer.SerializeToElement(details),
            occurredAt
        );

    internal static InventoryAuditEntry PermissionDenied(
        Guid organizationId,
        Guid actorUserId,
        string action,
        string subjectType,
        DateTimeOffset occurredAt
    ) =>
        new(
            Guid.CreateVersion7(occurredAt),
            organizationId,
            actorUserId,
            action,
            subjectType,
            Guid.Empty,
            InventoryAuditOutcomes.Denied,
            InventoryAuditReasonCodes.PermissionDenied,
            JsonSerializer.SerializeToElement(new { }),
            occurredAt
        );
}
