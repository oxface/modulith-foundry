namespace ModulithFoundry.Modules.Sales.ApprovalAuthorities;

internal sealed record ApprovalLimit
{
    private ApprovalLimit(decimal maximumAmount, string currency)
    {
        MaximumAmount = maximumAmount;
        Currency = currency;
    }

    internal decimal MaximumAmount { get; }
    internal string Currency { get; }

    internal static ApprovalLimit Create(decimal maximumAmount, string currency)
    {
        string normalizedCurrency = currency?.Trim().ToUpperInvariant() ?? "";
        if (normalizedCurrency is not ("USD" or "EUR"))
            throw new InvalidApprovalLimitException("Currency", "Use USD or EUR.");
        if (
            maximumAmount < 0
            || maximumAmount > 99_999_999_999_999_999.99m
            || decimal.Round(maximumAmount, 2) != maximumAmount
        )
            throw new InvalidApprovalLimitException(
                "MaximumAmount",
                "Use a nonnegative amount with at most two decimal places that fits decimal(19,2)."
            );
        return new(maximumAmount, normalizedCurrency);
    }
}
