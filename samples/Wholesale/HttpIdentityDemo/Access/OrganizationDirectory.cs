using ModulithFoundry.Tenancy;

namespace ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Access;

// Known Organizations are admitted to demonstration reads. This is not membership storage.
internal sealed class OrganizationDirectory
{
    private readonly Dictionary<string, TenantId> _tenants;

    public OrganizationDirectory(IConfiguration configuration)
    {
        _tenants = configuration
            .GetSection("Organizations")
            .GetChildren()
            .ToDictionary(
                entry => Required(entry, "Slug"),
                entry => new TenantId(Required(entry, "Id")),
                StringComparer.OrdinalIgnoreCase
            );
    }

    internal TenantId? Resolve(string slug) => _tenants.GetValueOrDefault(slug);

    private static string Required(IConfigurationSection entry, string name) =>
        !string.IsNullOrWhiteSpace(entry[name])
            ? entry[name]!
            : throw new InvalidOperationException(
                $"Configure {entry.Path}:{name} for the Organization directory."
            );
}
