namespace ModulithFoundry.Modules.Sales.Orders.ApproveSalesOrder;

internal enum OrderApprovalRejection
{
    SelfApproval = 1,
    AuthorityUnavailable = 2,
    CurrencyMismatch = 3,
    LimitExceeded = 4,
}
