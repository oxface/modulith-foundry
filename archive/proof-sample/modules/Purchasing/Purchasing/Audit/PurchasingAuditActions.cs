namespace ModulithFoundry.Modules.Purchasing.Audit;

internal static class PurchasingAuditActions
{
    internal const string RequestAccepted = "replenishment-request.accepted";
    internal const string RequestRejected = "replenishment-request.rejected";
    internal const string RequirementCreated = "replenishment-requirement.created";
    internal const string PurchaseOrderCreated = "purchase-order.created";
    internal const string PurchaseOrderLineSet = "purchase-order.line-set";
    internal const string PurchaseOrderIssued = "purchase-order.issued";
    internal const string PurchaseOrderCreateDenied = "purchase-order.create-denied";
    internal const string PurchaseOrderLineSetDenied = "purchase-order.line-set-denied";
    internal const string PurchaseOrderIssueDenied = "purchase-order.issue-denied";
}
