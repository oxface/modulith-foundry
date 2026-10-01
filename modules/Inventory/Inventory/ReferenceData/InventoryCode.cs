namespace ModulithFoundry.Modules.Inventory.ReferenceData;

internal static class InventoryCode
{
    internal static string Normalize(string value, string fieldName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidInventoryReferenceDataException(
                fieldName,
                $"{fieldName} must contain between 1 and {maximumLength} characters."
            );
        }

        string normalized = value.Trim().ToUpperInvariant();
        if (
            normalized.Length > maximumLength
            || !char.IsAsciiLetterOrDigit(normalized[0])
            || normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_' and not '.'
            )
        )
        {
            throw new InvalidInventoryReferenceDataException(
                fieldName,
                $"{fieldName} must start with an ASCII letter or digit and contain at most {maximumLength} ASCII letters, digits, hyphens, underscores, or periods."
            );
        }

        return normalized;
    }

    internal static string NormalizeText(string value, string fieldName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidInventoryReferenceDataException(
                fieldName,
                $"{fieldName} must contain between 1 and {maximumLength} characters."
            );
        }

        string normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new InvalidInventoryReferenceDataException(
                fieldName,
                $"{fieldName} must contain between 1 and {maximumLength} characters."
            );
        }

        return normalized;
    }
}

internal sealed class InvalidInventoryReferenceDataException(string field, string message)
    : ArgumentException(message)
{
    internal string Field { get; } = field;
}
