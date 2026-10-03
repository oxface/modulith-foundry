namespace ModulithFoundry.Modules.Purchasing.Contracts;

public abstract record GetReplenishmentRequirementResult
{
    private GetReplenishmentRequirementResult() { }

    public sealed record Found(ReplenishmentRequirementView Requirement)
        : GetReplenishmentRequirementResult;

    public sealed record NotFound : GetReplenishmentRequirementResult;

    public sealed record PermissionDenied : GetReplenishmentRequirementResult;
}
