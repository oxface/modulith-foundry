namespace ModulithFoundry.Modules.Sales.Contracts;

public abstract record GetSalesApprovalAuthorityResult
{
    private GetSalesApprovalAuthorityResult() { }

    public sealed record Found(SalesApprovalAuthorityView Authority)
        : GetSalesApprovalAuthorityResult;

    public sealed record NotFound : GetSalesApprovalAuthorityResult;

    public sealed record PermissionDenied : GetSalesApprovalAuthorityResult;
}
