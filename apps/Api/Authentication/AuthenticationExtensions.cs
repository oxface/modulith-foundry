using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ModulithFoundry.Modules.Access.Contracts;
using StackExchange.Redis;

namespace ModulithFoundry.Api.Authentication;

internal static class AuthenticationExtensions
{
    internal static IServiceCollection AddBffAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<OidcSettings>()
            .BindConfiguration(OidcSettings.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                static settings => IsValidAuthority(settings),
                "OIDC authority must be an absolute HTTP(S) URI with a host and no user info, query, or fragment; HTTPS is required when metadata HTTPS is required.")
            .ValidateOnStart();

        string redisConnectionString = configuration.GetConnectionString("redis")
            ?? throw new InvalidOperationException("Connection string 'redis' is required.");
        var redisConfiguration = ConfigurationOptions.Parse(redisConnectionString);
        redisConfiguration.AbortOnConnectFail = true;
        redisConfiguration.ConnectTimeout = 5_000;

        services.AddDataProtection()
            .SetApplicationName("ModulithFoundry");
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-modulith-foundry-antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.Path = "/";
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConfiguration));
        services.AddSingleton<RedisTicketStore>();
        services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>,
            CookieTicketStoreConfiguration>();
        services.AddHealthChecks().AddCheck<RedisHealthCheck>("redis");

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "__Host-modulith-foundry";
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = static context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = static context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            })
            .AddOpenIdConnect();

        services.AddOptions<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme)
            .Configure<IOptions<OidcSettings>>(static (options, configured) =>
                ConfigureOpenIdConnect(options, configured.Value));

        services.AddAuthorization();
        return services;
    }

    private static void ConfigureOpenIdConnect(
        OpenIdConnectOptions options,
        OidcSettings oidc)
    {
        options.Authority = oidc.Authority;
        options.ClientId = oidc.ClientId;
        options.ClientSecret = oidc.ClientSecret;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.ResponseMode = OpenIdConnectResponseMode.FormPost;
        options.UsePkce = true;
        options.SaveTokens = true;
        options.MapInboundClaims = false;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.RequireHttpsMetadata = oidc.RequireHttpsMetadata;
        options.Scope.Clear();
        options.Scope.Add(OpenIdConnectScope.OpenIdProfile);
        options.Scope.Add(OpenIdConnectScope.Email);
        // The OIDC handler deletes protocol claims by default. Keep the validated issuer because
        // issuer plus subject is the stable external-identity key used during JIT linking.
        options.ClaimActions.Remove("iss");
        options.TokenValidationParameters = OidcTokenValidation.Create(oidc.ClientId);
        options.Events.OnTicketReceived = LinkExternalIdentityAsync;
        options.Events.OnRemoteFailure = static context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
    }

    private static async Task LinkExternalIdentityAsync(TicketReceivedContext context)
    {
        ClaimsPrincipal principal = context.Principal
            ?? throw new InvalidOperationException("The completed OIDC principal is missing.");
        string issuer = principal.GetRequiredClaimValue("iss");
        string subject = principal.GetRequiredClaimValue("sub");
        ExternalIdentity identity = ExternalIdentity.Create(
            issuer,
            subject,
            principal.FindFirstValue("email"),
            principal.FindFirstValue("name"));
        UserIdentityLink linked = await context.HttpContext.RequestServices
            .GetRequiredService<IExternalIdentityLinker>()
            .LinkAsync(identity, context.HttpContext.RequestAborted);

        if (principal.Identity is not ClaimsIdentity claimsIdentity)
        {
            throw new InvalidOperationException("The validated OIDC identity is missing.");
        }

        claimsIdentity.AddClaim(new Claim(ProductClaims.UserId, linked.UserId.Value.ToString()));
    }

    private static bool IsValidAuthority(OidcSettings settings) =>
        Uri.TryCreate(settings.Authority, UriKind.Absolute, out Uri? authority)
        && (authority.Scheme == Uri.UriSchemeHttp || authority.Scheme == Uri.UriSchemeHttps)
        && !string.IsNullOrWhiteSpace(authority.Host)
        && string.IsNullOrEmpty(authority.UserInfo)
        && string.IsNullOrEmpty(authority.Query)
        && string.IsNullOrEmpty(authority.Fragment)
        && (!settings.RequireHttpsMetadata || authority.Scheme == Uri.UriSchemeHttps);
}

internal static class OidcTokenValidation
{
    internal static TokenValidationParameters Create(string clientId) =>
        new()
        {
            NameClaimType = "name",
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidAudience = clientId,
        };
}
