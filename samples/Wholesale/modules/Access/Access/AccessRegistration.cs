using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Samples.Wholesale.Access.Contracts;

namespace ModulithFoundry.Samples.Wholesale.Access;

public static class AccessRegistration
{
    public static IServiceCollection AddAccessQueries(this IServiceCollection services) =>
        services.AddScoped<IApplicationAccess, ApplicationAccess>();
}
