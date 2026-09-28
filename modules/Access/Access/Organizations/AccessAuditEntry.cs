using System.Text.Json;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Access.Organizations;

internal sealed class AccessAuditEntry : IOrganizationOwned
{
    private const string AccessSourceModule = "access";
    private const string SucceededOutcome = "succeeded";

    private AccessAuditEntry()
    {
        Action = null!;
        SubjectType = null!;
    }

    private AccessAuditEntry(
        Guid id,
        Guid organizationId,
        Guid actorUserId,
        string action,
        string subjectType,
        Guid subjectId,
        string outcome,
        string? reasonCode,
        string? correlationId,
        string? traceId,
        string sourceModule,
        short schemaVersion,
        JsonElement details,
        DateTimeOffset occurredAt)
    {
        Id = id;
        OrganizationId = organizationId;
        ActorUserId = actorUserId;
        Action = action;
        SubjectType = subjectType;
        SubjectId = subjectId;
        Outcome = outcome;
        ReasonCode = reasonCode;
        CorrelationId = correlationId;
        TraceId = traceId;
        SourceModule = sourceModule;
        SchemaVersion = schemaVersion;
        Details = details;
        OccurredAt = occurredAt;
    }

    internal Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    internal Guid ActorUserId { get; private set; }

    internal string Action { get; private set; }

    internal string SubjectType { get; private set; }

    internal Guid SubjectId { get; private set; }

    internal string Outcome { get; private set; } = null!;

    internal string? ReasonCode { get; private set; }

    internal string? CorrelationId { get; private set; }

    internal string? TraceId { get; private set; }

    internal string SourceModule { get; private set; } = null!;

    internal short SchemaVersion { get; private set; }

    internal JsonElement Details { get; private set; }

    internal DateTimeOffset OccurredAt { get; private set; }

    internal static AccessAuditEntry Create<TDetails>(
        Guid id,
        Guid organizationId,
        Guid actorUserId,
        string action,
        string subjectType,
        Guid subjectId,
        short schemaVersion,
        TDetails details,
        string? correlationId,
        string? traceId,
        DateTimeOffset occurredAt) =>
        new(
            id,
            organizationId,
            actorUserId,
            action,
            subjectType,
            subjectId,
            SucceededOutcome,
            reasonCode: null,
            correlationId,
            traceId,
            AccessSourceModule,
            schemaVersion,
            JsonSerializer.SerializeToElement(details),
            occurredAt);
}
