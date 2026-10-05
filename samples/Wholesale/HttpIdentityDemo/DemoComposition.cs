using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using ModulithFoundry.ActorIdentity;
using ModulithFoundry.ActorIdentity.AspNetCore;

namespace ModulithFoundry.Samples.Wholesale.HttpIdentityDemo;

public static class DemoComposition
{
    public static void AddIdentityServices(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        NativeAuthentication.Configure(services, configuration);
        services.AddAuthorization(options =>
        {
            var authenticated = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.DefaultPolicy = authenticated;
            options.FallbackPolicy = authenticated;
        });
        services.AddSingleton(new ExternalIdentityDirectory(configuration));
        services.AddHttpActorContext<DirectoryActorResolver>();
    }

    public static void ConfigureHttp(WebApplication app)
    {
        app.Use(
            async (context, next) =>
            {
                try
                {
                    await next(context);
                }
                catch (HttpActorResolutionException)
                {
                    await Results
                        .Problem(
                            statusCode: StatusCodes.Status403Forbidden,
                            title: "Actor mapping failed."
                        )
                        .ExecuteAsync(context);
                }
            }
        );
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseHttpActorContext();
        app.MapGet("/health", () => TypedResults.Ok("healthy")).AllowAnonymous();
        app.MapGet("/identity", ReadIdentity).RequireAuthorization();
        app.MapGet("/public-identity", ReadIdentity).AllowAnonymous();
        app.MapGet(
                "/login",
                () =>
                    Results.Challenge(
                        new AuthenticationProperties { RedirectUri = "/identity" },
                        [NativeAuthentication.OidcScheme]
                    )
            )
            .AllowAnonymous();
    }

    private static Ok<IdentityResponse> ReadIdentity(IActorContextAccessor accessor)
    {
        Actor actor = accessor.Current.Actor;
        return TypedResults.Ok(new IdentityResponse(actor.Kind, actor.Id?.Value));
    }
}
