namespace ModulithFoundry.Modules.Sales.Orders;

internal sealed class InvalidSalesOrderInputException(string field, string detail)
    : ArgumentException(detail)
{
    internal string Field { get; } = field;
}
