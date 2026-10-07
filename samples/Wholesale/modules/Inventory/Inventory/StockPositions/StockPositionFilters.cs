namespace ModulithFoundry.Samples.Wholesale.Inventory.StockPositions;

internal static class StockPositionFilters
{
    internal static IQueryable<StockPositionCurrentRow> WithOnHandAtLeast(
        this IQueryable<StockPositionCurrentRow> rows,
        decimal quantity
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        return rows.Where(row => row.State.GetProperty("onHand").GetDecimal() >= quantity);
    }
}
