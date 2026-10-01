namespace ModulithFoundry.Modules.Inventory.StockPositions;

internal readonly record struct Quantity
{
    private const decimal Maximum = 9_999_999_999_999.999999m;

    private Quantity(decimal value)
    {
        Value = value;
    }

    internal decimal Value { get; }

    internal static Quantity NonNegative(decimal value) => Create(value, requirePositive: false);

    // Recorded facts and intermediate projection state are not new command inputs.
    internal static Quantity Restore(decimal value) => new(value);

    internal Quantity ApplyRecordedIncrease(decimal value) => Restore(checked(Value + value));

    internal static Quantity Positive(decimal value) => Create(value, requirePositive: true);

    internal Quantity Add(Quantity other)
    {
        decimal sum = checked(Value + other.Value);
        return NonNegative(sum);
    }

    private static Quantity Create(decimal value, bool requirePositive)
    {
        if (value < 0m || (requirePositive && value == 0m))
        {
            throw new InvalidStockPositionValueException(
                "quantity",
                requirePositive
                    ? "Quantity must be greater than zero."
                    : "Quantity cannot be negative."
            );
        }

        if (value > Maximum || decimal.Round(value, 6) != value)
        {
            throw new InvalidStockPositionValueException(
                "quantity",
                "Quantity must have at most 13 integral and 6 fractional digits."
            );
        }

        return new Quantity(value);
    }
}

internal sealed class InvalidStockPositionValueException(string field, string message)
    : ArgumentException(message)
{
    internal string Field { get; } = field;
}
