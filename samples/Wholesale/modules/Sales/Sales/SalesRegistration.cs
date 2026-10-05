using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Samples.Wholesale.Sales.Contracts;

namespace ModulithFoundry.Samples.Wholesale.Sales;

public static class SalesRegistration
{
    public static IServiceCollection AddCustomerProfiles(this IServiceCollection services) =>
        services.AddScoped<ICustomerProfiles, CustomerProfiles>();
}
