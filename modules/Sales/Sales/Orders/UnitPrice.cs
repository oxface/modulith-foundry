namespace ModulithFoundry.Modules.Sales.Orders;

internal readonly record struct UnitPrice
{
    private UnitPrice(decimal value)
    {
        Value = value;
    }

    internal decimal Value { get; }

    internal static UnitPrice Create(decimal value)
    {
        if (value < 0m || value > 999_999_999_999_999.9999m || decimal.Round(value, 4) != value)
        {
            throw new InvalidSalesOrderInputException("unitPrice", "Unit price must be non-negative and fit 15 integer and 4 fractional digits.");
        }

        return new(value);
    }
}
