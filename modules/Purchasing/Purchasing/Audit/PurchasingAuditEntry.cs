using System.Text.Json;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Purchasing.Audit;

internal sealed class PurchasingAuditEntry : IOrganizationOwned
{
    private PurchasingAuditEntry()
    {
        Action = null!;
        Outcome = null!;
    }

    internal Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal string Action { get; private set; }
    internal Guid SubjectId { get; private set; }
    internal string Outcome { get; private set; }
    internal string? ReasonCode { get; private set; }
    internal string SystemActor { get; private set; } = "sales.order-fulfilment";
    internal Guid? ActorUserId { get; private set; }
    internal string SubjectType { get; private set; } = PurchasingAuditSubjects.Request;
    internal string SourceModule { get; private set; } = "purchasing";
    internal short SchemaVersion { get; private set; } = 1;
    internal JsonElement Details { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }

    internal static PurchasingAuditEntry Record(
        Guid organizationId,
        string action,
        Guid subjectId,
        string outcome,
        string? reasonCode,
        object details,
        DateTimeOffset now,
        string subjectType = PurchasingAuditSubjects.Request
    ) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            OrganizationId = organizationId,
            Action = action,
            SubjectId = subjectId,
            Outcome = outcome,
            ReasonCode = reasonCode,
            Details = JsonSerializer.SerializeToElement(details),
            OccurredAt = now,
            SubjectType = subjectType,
        };
}
