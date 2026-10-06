using Microsoft.EntityFrameworkCore;

namespace ModulithFoundry.Samples.Wholesale.Inventory.StockPositions;

internal sealed class StockPositionInlineProjection(InventoryDbContext database)
{
    internal async Task<StockPositionCurrentRow> LoadAsync(
        EventStream stream,
        CancellationToken cancellationToken
    )
    {
        if (stream.Version < 1 || stream.CreatedAt > stream.UpdatedAt)
            throw new InvalidDataException("The stock-position stream header is invalid.");
        var row = await database
            .StockPositions.AsNoTracking()
            .SingleOrDefaultAsync(row => row.StreamId == stream.Id, cancellationToken);
        if (row is null || row.Version < stream.Version)
            throw new InvalidDataException(
                "The required stock-position view is missing or behind its stream."
            );
        if (row.Version > stream.Version)
            throw new DbUpdateConcurrencyException(
                "The stock-position view advanced after the header read."
            );
        if (row.RecordedAt != stream.UpdatedAt)
            throw new InvalidDataException(
                "The stock-position view and stream timestamps disagree."
            );
        return row;
    }

    internal void Stage(StockPositionCurrentRow? current, StockPositionCurrentRow next)
    {
        if (current is null)
            database.StockPositions.Add(next);
        else
        {
            database.StockPositions.Attach(current);
            database.Entry(current).CurrentValues.SetValues(next);
        }
    }
}
