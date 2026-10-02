namespace ModulithFoundry.Modules.Purchasing.Replenishment;

internal readonly record struct ReplenishmentQuantity
{
    private ReplenishmentQuantity(decimal value) => Value = value;

    internal decimal Value { get; }

    internal static ReplenishmentQuantity Create(decimal value)
    {
        if (value <= 0 || value > 9_999_999_999_999.999999m || decimal.Round(value, 6) != value)
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Quantity must be positive with at most six decimal places."
            );
        return new(value);
    }
}
