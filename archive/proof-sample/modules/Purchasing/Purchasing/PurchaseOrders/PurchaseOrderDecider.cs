using ModulithFoundry.Modules.Purchasing.PurchaseOrders.Events;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders;

internal static class PurchaseOrderDecider
{
    internal static IPurchaseOrderEvent Draft(
        string code,
        string supplierReference,
        string currency
    )
    {
        currency = Code(currency, "currency", 3);
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetter))
            throw new InvalidPurchaseOrderValueException(
                "currency",
                "Currency must be a three-letter code."
            );
        return new PurchaseOrderDrafted(
            Code(code, "code", 32),
            Code(supplierReference, "supplierReference", 64),
            currency
        );
    }

    internal static IReadOnlyList<IPurchaseOrderEvent> SetLine(
        PurchaseOrderState state,
        string itemCode,
        decimal quantity,
        decimal unitPrice
    )
    {
        RequireDraft(state);
        itemCode = Code(itemCode, "itemCode", 64);
        if (quantity is <= 0 or > 10000 || decimal.Round(quantity, 3) != quantity)
            throw new InvalidPurchaseOrderValueException(
                "quantity",
                "Quantity must be positive, at most 10000 and have at most three decimal places."
            );
        if (unitPrice is < 0 or > 1000000 || decimal.Round(unitPrice, 2) != unitPrice)
            throw new InvalidPurchaseOrderValueException(
                "unitPrice",
                "Unit price must be nonnegative, at most 1000000 and have at most two decimal places."
            );
        var existing = state.Lines.SingleOrDefault(x => x.ItemCode == itemCode);
        if (
            existing is not null
            && existing.Quantity == quantity
            && existing.UnitPrice == unitPrice
        )
            return [];
        if (existing is null && state.Lines.Count >= 100)
            throw new PurchaseOrderDecisionException("A draft supports at most 100 lines.");
        return [new PurchaseOrderLineSet(itemCode, quantity, unitPrice)];
    }

    internal static IReadOnlyList<IPurchaseOrderEvent> Issue(PurchaseOrderState state)
    {
        RequireDraft(state);
        if (state.Lines.Count == 0)
            throw new PurchaseOrderDecisionException("An empty Purchase Order cannot be issued.");
        return [new PurchaseOrderIssued()];
    }

    internal static string Code(string value, string field, int maximum)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (
            string.IsNullOrEmpty(normalized)
            || normalized.Length > maximum
            || normalized.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')
        )
            throw new InvalidPurchaseOrderValueException(
                field,
                $"{field} must contain 1–{maximum} letters, digits or hyphens."
            );
        return normalized;
    }

    private static void RequireDraft(PurchaseOrderState state)
    {
        if (state.Status != PurchaseOrderStatus.Draft)
            throw new PurchaseOrderDecisionException(
                "An issued Purchase Order cannot be edited or issued again."
            );
    }
}

internal sealed class InvalidPurchaseOrderValueException(string field, string message)
    : ArgumentException(message)
{
    internal string Field { get; } = field;
}

internal sealed class PurchaseOrderDecisionException(string message) : Exception(message);
