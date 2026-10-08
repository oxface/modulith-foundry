namespace Rootbolt.Tenancy.AspNetCore;

public sealed class HttpTenantContextOptions
{
    public TenantRequirement DefaultRequirement { get; set; } = TenantRequirement.Required;
}
