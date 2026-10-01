namespace ModulithFoundry.Modules.Sales.Orders;

internal static class OrderAmount
{
    internal const decimal Maximum = 99_999_999_999_999_999.99m;

    internal static decimal Calculate(OrderQuantity quantity, UnitPrice price)
    {
        decimal amount = decimal.Round(quantity.Value * price.Value, 2, MidpointRounding.AwayFromZero);
        if (amount > Maximum)
        {
            throw new InvalidSalesOrderInputException("lines", "The line amount exceeds the supported range.");
        }
        return amount;
    }
}
