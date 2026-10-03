namespace ModulithFoundry.Modules.Sales.Contracts;

public abstract record RevokeSalesApprovalAuthorityResult
{
    private RevokeSalesApprovalAuthorityResult() { }

    public sealed record Revoked(SalesApprovalAuthorityView Authority)
        : RevokeSalesApprovalAuthorityResult;

    public sealed record Unchanged(SalesApprovalAuthorityView Authority)
        : RevokeSalesApprovalAuthorityResult;

    public sealed record InvalidExpectedVersion : RevokeSalesApprovalAuthorityResult;

    public sealed record NotFound : RevokeSalesApprovalAuthorityResult;

    public sealed record VersionConflict : RevokeSalesApprovalAuthorityResult;

    public sealed record PermissionDenied : RevokeSalesApprovalAuthorityResult;
}
