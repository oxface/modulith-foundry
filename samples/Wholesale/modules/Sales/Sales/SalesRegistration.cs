using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModulithFoundry.Samples.Wholesale.Sales.Contracts;
using Rootbolt.Auditing.EntityFrameworkCore;

namespace ModulithFoundry.Samples.Wholesale.Sales;

public static class SalesRegistration
{
    public static IServiceCollection AddCustomerProfiles(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAudit<SalesDbContext>, EfAudit<SalesDbContext>>();
        services.AddScoped<SalesAudit>();
        return services.AddScoped<ICustomerProfiles, CustomerProfiles>();
    }
}
