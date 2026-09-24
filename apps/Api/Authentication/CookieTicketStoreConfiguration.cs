using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace ModulithFoundry.Api.Authentication;

internal sealed class CookieTicketStoreConfiguration(RedisTicketStore ticketStore)
    : IPostConfigureOptions<CookieAuthenticationOptions>
{
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        if (name == CookieAuthenticationDefaults.AuthenticationScheme)
        {
            options.SessionStore = ticketStore;
        }
    }
}
