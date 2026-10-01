using ModulithFoundry.Modules.Sales.ApprovalAuthorities;

namespace ModulithFoundry.Modules.Sales.Orders.ApproveSalesOrder;

internal static class OrderApprovalPolicy
{
    internal static OrderApprovalRejection? Evaluate(
        SalesOrder order,
        Guid actorUserId,
        SalesApprovalAuthority? authority
    )
    {
        if (order.SubmittedBy == actorUserId)
            return OrderApprovalRejection.SelfApproval;
        if (authority is null || !authority.IsEnabled)
            return OrderApprovalRejection.AuthorityUnavailable;
        if (authority.Currency != order.Currency)
            return OrderApprovalRejection.CurrencyMismatch;
        return authority.MaximumAmount < order.TotalAmount
            ? OrderApprovalRejection.LimitExceeded
            : null;
    }
}
