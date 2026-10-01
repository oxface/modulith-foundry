namespace ModulithFoundry.Modules.Sales.Customers;

internal static class CustomerInput
{
    internal const int CodeMaximumLength = 64;
    internal const int NameMaximumLength = 200;

    internal static string NormalizeCode(string code)
    {
        string normalized = code?.Trim().ToUpperInvariant() ?? "";
        if (normalized.Length is < 1 or > CodeMaximumLength
            || !char.IsAsciiLetterOrDigit(normalized[0])
            || normalized.Any(character => !char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_' and not '.'))
        {
            throw new InvalidCustomerInputException("code",
                "Customer code must start with an ASCII letter or digit and contain at most 64 ASCII letters, digits, hyphens, underscores, or periods.");
        }
        return normalized;
    }

    internal static string NormalizeName(string name)
    {
        string normalized = name?.Trim() ?? "";
        if (normalized.Length is < 1 or > NameMaximumLength || normalized.Any(char.IsControl))
        {
            throw new InvalidCustomerInputException("name", "Customer name must contain 1–200 characters without control characters.");
        }
        return normalized;
    }
}

internal sealed class InvalidCustomerInputException(string field, string detail) : ArgumentException(detail)
{
    internal string Field { get; } = field;
}
