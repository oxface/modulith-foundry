namespace ModulithFoundry.Modules.Purchasing.Contracts;

public sealed record ReplenishmentRequestView(
    Guid OperationId,
    string Status,
    Guid? RequirementId,
    string? ReasonCode
);
