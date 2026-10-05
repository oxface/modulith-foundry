using Microsoft.EntityFrameworkCore;
using ModulithFoundry.ActorIdentity.AspNetCore;
using ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Access.Contracts;
using ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Access.Persistence;

namespace ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Access;

public static class AccessRegistration
{
    public static IServiceCollection AddApplicationAccess(
        this IServiceCollection services,
        string connectionString
    )
    {
        services.AddDbContext<AccessDbContext>(options =>
            AccessDatabase.Configure(options, connectionString)
        );
        services.AddScoped<IApplicationAccess, ApplicationAccess>();
        return services.AddHttpActorContext<ApplicationActorResolver>();
    }
}
