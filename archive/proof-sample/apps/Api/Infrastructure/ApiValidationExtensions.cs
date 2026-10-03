namespace ModulithFoundry.Api.Infrastructure;

internal static class ApiValidationExtensions
{
    internal static IServiceCollection AddApiRequestValidation(this IServiceCollection services) =>
        services.AddValidation();
}
