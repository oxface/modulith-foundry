namespace ModulithFoundry.Modules.Sales.Audit;

internal static class SalesAuditActions
{
    internal const string FulfilmentQueued = "order-fulfilment.queued";
    internal const string ReservationOutcomeRecorded =
        "order-fulfilment.reservation-outcome-recorded";
    internal const string ReservationOutcomeIgnored =
        "order-fulfilment.reservation-outcome-ignored";
    internal const string OrderApproved = "sales-order.approved";
    internal const string OrderApproveDenied = "sales-order.approve-denied";
    internal const string ApprovalAuthoritySet = "approval-authority.set";
    internal const string ApprovalAuthoritySetDenied = "approval-authority.set-denied";
    internal const string ApprovalAuthorityRevoked = "approval-authority.revoked";
    internal const string ApprovalAuthorityRevokeDenied = "approval-authority.revoke-denied";
    internal const string CustomerCreated = "customer.created";
    internal const string OrderCreated = "sales-order.created";
    internal const string OrderSubmitted = "sales-order.submitted";
    internal const string OrderSubmitDenied = "sales-order.submit-denied";
    internal const string OrderCreateDenied = "sales-order.create-denied";
    internal const string CustomerCreateDenied = "customer.create-denied";
}
