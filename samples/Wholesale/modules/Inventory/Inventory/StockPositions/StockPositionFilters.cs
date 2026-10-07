namespace ModulithFoundry.Samples.Wholesale.Inventory.StockPositions;

internal static class StockPositionFilters
{
    internal static IQueryable<StockPositionStateRow> WithOnHandAtLeast(
        this IQueryable<StockPositionStateRow> rows,
        decimal quantity
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        return rows.Where(row => row.State.GetProperty("onHand").GetDecimal() >= quantity);
    }
}
