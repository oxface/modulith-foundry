namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.Events;

internal interface IPurchaseOrderEvent;

[StoredEventType("purchasing.purchase-order.drafted", 1)]
internal sealed record PurchaseOrderDrafted(string Code, string SupplierReference, string Currency)
    : IPurchaseOrderEvent;

[StoredEventType("purchasing.purchase-order.line-set", 1)]
internal sealed record PurchaseOrderLineSet(string ItemCode, decimal Quantity, decimal UnitPrice)
    : IPurchaseOrderEvent;

[StoredEventType("purchasing.purchase-order.issued", 1)]
internal sealed record PurchaseOrderIssued : IPurchaseOrderEvent;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class StoredEventTypeAttribute(string name, int schemaVersion) : Attribute
{
    internal string Name { get; } = name;
    internal int SchemaVersion { get; } = schemaVersion;
}
