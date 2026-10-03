using System.Diagnostics;

namespace ModulithFoundry.Api.Infrastructure;

internal static class ApiErrorHandlingExtensions
{
    internal static IServiceCollection AddApiErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions.TryAdd(
                    "traceId",
                    Activity.Current?.Id ?? context.HttpContext.TraceIdentifier
                );
            };
        });
        return services;
    }

    internal static WebApplication UseApiErrorHandling(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler();
        }

        app.UseStatusCodePages();
        return app;
    }
}
