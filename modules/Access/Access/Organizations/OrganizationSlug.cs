using System.Text;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Access.Organizations;

internal readonly record struct OrganizationSlug
{
    private const int MinimumLength = 3;
    private const int MaximumLength = 63;

    private OrganizationSlug(string value)
    {
        Value = value;
    }

    internal string Value { get; }

    internal static OrganizationSlug Create(string proposedSlug)
    {
        if (string.IsNullOrWhiteSpace(proposedSlug))
        {
            throw new InvalidOrganizationSlugException(
                $"An organization slug must contain between {MinimumLength} and {MaximumLength} canonical characters.");
        }

        var canonical = new StringBuilder(proposedSlug.Length);
        var pendingSeparator = false;

        foreach (char character in proposedSlug.Trim())
        {
            if (character is >= 'A' and <= 'Z')
            {
                AppendSeparatorIfNeeded(canonical, ref pendingSeparator);
                canonical.Append(char.ToLowerInvariant(character));
            }
            else if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                AppendSeparatorIfNeeded(canonical, ref pendingSeparator);
                canonical.Append(character);
            }
            else if (character == '-' || character == '_' || char.IsWhiteSpace(character))
            {
                pendingSeparator = canonical.Length > 0;
            }
            else
            {
                throw new InvalidOrganizationSlugException(
                    "An organization slug may contain only ASCII letters, digits, hyphens, underscores, and spaces.");
            }
        }

        string value = canonical.ToString();
        if (value.Length is < MinimumLength or > MaximumLength)
        {
            throw new InvalidOrganizationSlugException(
                $"An organization slug must contain between {MinimumLength} and {MaximumLength} canonical characters.");
        }

        return new OrganizationSlug(value);
    }

    private static void AppendSeparatorIfNeeded(StringBuilder value, ref bool pendingSeparator)
    {
        if (pendingSeparator)
        {
            value.Append('-');
            pendingSeparator = false;
        }
    }
}
