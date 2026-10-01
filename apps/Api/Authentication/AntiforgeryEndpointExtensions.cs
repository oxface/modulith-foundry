using Microsoft.AspNetCore.Antiforgery;

namespace ModulithFoundry.Api.Authentication;

internal static class AntiforgeryEndpointExtensions
{
    internal static RouteHandlerBuilder RequireBffAntiforgery(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(
            async (context, next) =>
            {
                IAntiforgery antiforgery =
                    context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
                if (!await antiforgery.IsRequestValidAsync(context.HttpContext))
                {
                    return Results.BadRequest();
                }

                return await next(context);
            }
        );
}
