using ModulithFoundry.Tenancy.AspNetCore;

namespace ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Access;

// Template-local registration keeps Access implementation types inside the owning sample module.
public static class OrganizationTenancyExtensions
{
    public static IServiceCollection AddOrganizationTenancyFromRoute(
        this IServiceCollection services,
        string routeValueName
    )
    {
        services.AddSingleton<OrganizationDirectory>();
        return services.AddRouteTenancy<OrganizationTenantResolver>(routeValueName);
    }

    public static IServiceCollection AddOrganizationTenancyFromSubdomain(
        this IServiceCollection services,
        string baseDomain
    )
    {
        services.AddSingleton<OrganizationDirectory>();
        return services.AddSubdomainTenancy<OrganizationTenantResolver>(baseDomain);
    }
}
