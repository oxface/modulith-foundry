using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;

namespace ModulithFoundry.Modules.Purchasing.Replenishment.Requests;

// Durable request and compact retained business-operation identity, not a broker lease.
internal sealed class ReplenishmentRequest : IOrganizationOwned
{
    private ReplenishmentRequest()
    {
        BaseUnitCode = null!;
        Fingerprint = null!;
    }

    internal Guid OperationId { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal Guid FirstMessageId { get; private set; }
    internal Guid ProcessId { get; private set; }
    internal long OrderNumber { get; private set; }
    internal int LineNumber { get; private set; }
    internal Guid StockItemId { get; private set; }
    internal decimal Quantity { get; private set; }
    internal string BaseUnitCode { get; private set; }
    internal long MinimumReferenceRevision { get; private set; }
    internal string Fingerprint { get; private set; }
    internal DateTimeOffset ReceivedAt { get; private set; }
    internal DateTimeOffset NextAttemptAt { get; private set; }
    internal Guid? RequirementId { get; private set; }
    internal string? ReasonCode { get; private set; }
    internal bool IsPending => RequirementId is null && ReasonCode is null;
    internal string Status =>
        ReasonCode is not null ? ReplenishmentRequestStatuses.Rejected
        : RequirementId is not null ? ReplenishmentRequestStatuses.Completed
        : ReplenishmentRequestStatuses.PendingReference;

    internal static ReplenishmentRequest Create(
        CreateReplenishmentRequirementV1 command,
        string fingerprint,
        DateTimeOffset now
    ) =>
        new()
        {
            OperationId = command.OperationId,
            OrganizationId = command.OrganizationId,
            FirstMessageId = command.MessageId,
            ProcessId = command.ProcessId,
            OrderNumber = command.OrderNumber,
            LineNumber = command.LineNumber,
            StockItemId = command.StockItemId,
            Quantity = command.Quantity,
            BaseUnitCode = command.BaseUnitCode,
            MinimumReferenceRevision = command.MinimumReferenceRevision,
            Fingerprint = fingerprint,
            ReceivedAt = now,
            NextAttemptAt = now,
        };

    internal void Wait(DateTimeOffset now) => NextAttemptAt = now.AddSeconds(5);

    internal void Complete(Guid requirementId)
    {
        if (!IsPending)
            throw new InvalidOperationException("The replenishment request is already settled.");
        RequirementId = requirementId;
    }

    internal void Reject(string reasonCode)
    {
        if (!IsPending)
            throw new InvalidOperationException("The replenishment request is already settled.");
        ReasonCode = reasonCode;
    }
}
