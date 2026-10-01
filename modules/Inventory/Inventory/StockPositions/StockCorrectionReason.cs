namespace ModulithFoundry.Modules.Inventory.StockPositions;

internal readonly record struct StockCorrectionReason
{
    private StockCorrectionReason(string value) => Value = value;

    internal string Value { get; }

    internal static StockCorrectionReason Create(string value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 200 || normalized.Any(char.IsControl))
        {
            throw new InvalidStockPositionValueException(
                "reason", "Correction reason must contain 1 to 200 characters without control characters.");
        }

        return new(normalized);
    }
}
