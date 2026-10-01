namespace ModulithFoundry.Modules.Sales.Orders;

internal sealed class SalesOrderLine
{
    private SalesOrderLine()
    {
        Sku = null!;
        Description = null!;
        BaseUnitCode = null!;
    }

    private SalesOrderLine(int lineNumber, SalesOrderLineInput input, OrderQuantity quantity, UnitPrice unitPrice)
    {
        LineNumber = lineNumber;
        StockItemId = input.StockItemId;
        Sku = input.Sku;
        Description = input.Description;
        BaseUnitCode = input.BaseUnitCode;
        Quantity = quantity.Value;
        UnitPrice = unitPrice.Value;
        LineAmount = OrderAmount.Calculate(quantity, unitPrice);
    }

    internal int LineNumber { get; private set; }
    internal Guid StockItemId { get; private set; }
    internal string Sku { get; private set; }
    internal string Description { get; private set; }
    internal string BaseUnitCode { get; private set; }
    internal decimal Quantity { get; private set; }
    internal decimal UnitPrice { get; private set; }
    internal decimal LineAmount { get; private set; }

    internal static SalesOrderLine Create(int lineNumber, SalesOrderLineInput input) =>
        new(lineNumber, input, OrderQuantity.Create(input.Quantity), Orders.UnitPrice.Create(input.UnitPrice));
}
