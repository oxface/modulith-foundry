using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModulithFoundry.Samples.Wholesale.Sales.Contracts;
using Rootbolt.ActorIdentity;
using Rootbolt.Auditing.EntityFrameworkCore;
using Rootbolt.Messaging.EntityFrameworkCore;
using Rootbolt.Messaging.EntityFrameworkCore.Postgres;

namespace ModulithFoundry.Samples.Wholesale.Sales;

public static class SalesRegistration
{
    /// <summary>Registers Sales requests and reply processing without starting workers or configuring transport.</summary>
    public static IServiceCollection AddStockIssueRequests(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ActorContextAccessor>();
        services.TryAddScoped<IActorContextAccessor>(provider =>
            provider.GetRequiredService<ActorContextAccessor>()
        );
        services.TryAddScoped<IActorContextInitializer>(provider =>
            provider.GetRequiredService<ActorContextAccessor>()
        );
        services.AddPostgresOutbox<SalesDbContext>();
        services.AddPostgresInbox<SalesDbContext>();
        services.AddPostgresInboxProcessor<SalesDbContext>(new(TimeSpan.FromSeconds(1)));
        services.AddInboxHandler<SalesDbContext, StockIssues.StockIssueReplyHandler>(
            StockIssueReplyAdmission.Subscription
        );
        services.AddScoped<StockIssueDeadlines>();
        return services.AddScoped<IStockIssueRequests, StockIssues.StockIssueRequests>();
    }

    public static IServiceCollection AddCustomerProfiles(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAudit<SalesDbContext>, EfAudit<SalesDbContext>>();
        services.AddScoped<SalesAudit>();
        return services.AddScoped<ICustomerProfiles, CustomerProfiles>();
    }
}
