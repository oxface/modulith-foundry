namespace ModulithFoundry.Modules.Purchasing.Contracts;

public abstract record ListReplenishmentRequirementsResult
{
    private ListReplenishmentRequirementsResult() { }

    public sealed record Listed(IReadOnlyList<ReplenishmentRequirementView> Requirements)
        : ListReplenishmentRequirementsResult;

    public sealed record Invalid(string Detail) : ListReplenishmentRequirementsResult;

    public sealed record PermissionDenied : ListReplenishmentRequirementsResult;
}
