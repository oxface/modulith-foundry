using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MimeKit;
using ModulithFoundry.Modules.Access.Email;
using ModulithFoundry.Modules.Access.ExtensionPoints;
using ModulithFoundry.Modules.Access.Invitations;
using ModulithFoundry.Modules.Access.Invitations.Email;

namespace ModulithFoundry.Modules.Access.Composition;

internal static class InvitationEmailRegistration
{
    internal static IServiceCollection AddInvitationEmailDelivery(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AddInvitationOptions(services, configuration);
        AddSmtpOptions(services, configuration);
        services.TryAddScoped<IEmailTransport, SmtpEmailTransport>();
        services.AddScoped<InvitationEmailPayloadCodec>();
        services.AddScoped<InvitationEmailDispatcher>();
        services.AddHostedService<InvitationEmailDeliveryWorker>();
        return services;
    }

    private static void AddInvitationOptions(
        IServiceCollection services,
        IConfiguration configuration)
    {
        string? publicApplicationUrl = configuration[
            $"{InvitationOptions.SectionName}:PublicApplicationUrl"];
        string? lifetimeHours = configuration[$"{InvitationOptions.SectionName}:LifetimeHours"];
        services.AddOptions<InvitationOptions>()
            .Configure(options =>
            {
                options.PublicApplicationUrl = Uri.TryCreate(
                    publicApplicationUrl,
                    UriKind.Absolute,
                    out Uri? configuredBaseUrl)
                    ? configuredBaseUrl
                    : null;
                if (int.TryParse(lifetimeHours, out int configuredLifetimeHours))
                {
                    options.LifetimeHours = configuredLifetimeHours;
                }
            })
            .Validate(
                static options => IsValidPublicApplicationUrl(options.PublicApplicationUrl),
                "Public application URL must be an absolute HTTPS URL (HTTP is allowed only for loopback), without user info, query, or fragment.")
            .Validate(
                static options => options.LifetimeHours is >= 1 and <= 720,
                "Invitation lifetime must be between 1 and 720 hours.")
            .ValidateOnStart();
    }

    private static void AddSmtpOptions(
        IServiceCollection services,
        IConfiguration configuration)
    {
        string? host = configuration[$"{SmtpOptions.SectionName}:Host"];
        string? port = configuration[$"{SmtpOptions.SectionName}:Port"];
        string? security = configuration[$"{SmtpOptions.SectionName}:Security"];
        string? username = configuration[$"{SmtpOptions.SectionName}:Username"];
        string? password = configuration[$"{SmtpOptions.SectionName}:Password"];
        string? fromAddress = configuration[$"{SmtpOptions.SectionName}:FromAddress"];
        string? fromName = configuration[$"{SmtpOptions.SectionName}:FromName"];
        services.AddOptions<SmtpOptions>()
            .Configure(options =>
            {
                options.Host = host!;
                options.Port = int.TryParse(port, out int configuredPort)
                    ? configuredPort
                    : 0;
                options.Security = Enum.TryParse(
                    security,
                    ignoreCase: true,
                    out SmtpSecurity configuredSecurity)
                    ? configuredSecurity
                    : (SmtpSecurity)(-1);
                options.Username = username;
                options.Password = password;
                options.FromAddress = fromAddress!;
                if (!string.IsNullOrWhiteSpace(fromName))
                {
                    options.FromName = fromName.Trim();
                }
            })
            .Validate(
                static options => !string.IsNullOrWhiteSpace(options.Host)
                    && options.Port is >= 1 and <= 65535
                    && Enum.IsDefined(options.Security)
                    && MailboxAddress.TryParse(options.FromAddress, out _),
                "SMTP host, port, security mode, and sender address must be configured.")
            .Validate(
                static options => string.IsNullOrWhiteSpace(options.Username)
                    == string.IsNullOrWhiteSpace(options.Password),
                "SMTP username and password must either both be configured or both be absent.")
            .ValidateOnStart();
    }

    private static bool IsValidPublicApplicationUrl(Uri? uri) =>
        uri is { IsAbsoluteUri: true }
        && (uri.Scheme == Uri.UriSchemeHttps
            || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
        && !string.IsNullOrWhiteSpace(uri.Host)
        && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);
}
