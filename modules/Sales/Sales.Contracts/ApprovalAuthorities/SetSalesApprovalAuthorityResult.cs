namespace ModulithFoundry.Modules.Sales.Contracts;

public abstract record SetSalesApprovalAuthorityResult
{
    private SetSalesApprovalAuthorityResult() { }

    public sealed record Saved(SalesApprovalAuthorityView Authority)
        : SetSalesApprovalAuthorityResult;

    public sealed record Invalid(string Field, string Detail) : SetSalesApprovalAuthorityResult;

    public sealed record MembershipUnavailable : SetSalesApprovalAuthorityResult;

    public sealed record VersionConflict : SetSalesApprovalAuthorityResult;

    public sealed record PermissionDenied : SetSalesApprovalAuthorityResult;
}
