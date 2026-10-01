namespace ModulithFoundry.Modules.Sales.Orders;

internal readonly record struct OrderQuantity
{
    private OrderQuantity(decimal value)
    {
        Value = value;
    }

    internal decimal Value { get; }

    internal static OrderQuantity Create(decimal value)
    {
        if (value <= 0m || value > 9_999_999_999_999.999999m || decimal.Round(value, 6) != value)
        {
            throw new InvalidSalesOrderInputException("quantity", "Quantity must be positive and fit 13 integer and 6 fractional digits.");
        }

        return new(value);
    }
}
