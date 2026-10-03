namespace ModulithFoundry.Modules.Sales.Contracts;

public abstract record ApproveSalesOrderResult
{
    private ApproveSalesOrderResult() { }

    public sealed record Approved(SalesOrderView Order) : ApproveSalesOrderResult;

    public sealed record InvalidExpectedVersion : ApproveSalesOrderResult;

    public sealed record NotFound : ApproveSalesOrderResult;

    public sealed record NotAwaitingApproval : ApproveSalesOrderResult;

    public sealed record VersionConflict : ApproveSalesOrderResult;

    public sealed record PermissionDenied : ApproveSalesOrderResult;

    public sealed record SelfApprovalDenied : ApproveSalesOrderResult;

    public sealed record AuthorityUnavailable : ApproveSalesOrderResult;

    public sealed record CurrencyMismatch : ApproveSalesOrderResult;

    public sealed record LimitExceeded : ApproveSalesOrderResult;
}
