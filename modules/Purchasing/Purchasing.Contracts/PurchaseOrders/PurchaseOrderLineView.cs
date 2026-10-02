namespace ModulithFoundry.Modules.Purchasing.Contracts;

public sealed record PurchaseOrderLineView(string ItemCode, decimal Quantity, decimal UnitPrice);
