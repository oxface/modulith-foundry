using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using ModulithFoundry.Api.Infrastructure;
using StackExchange.Redis;

namespace ModulithFoundry.Api.Authentication;

internal sealed class RedisTicketStore : ITicketStore
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(8);
    private readonly string keyPrefix;
    private readonly IDataProtector protector;
    private readonly IDatabase redis;
    private readonly TimeProvider timeProvider;

    public RedisTicketStore(
        IConnectionMultiplexer connection,
        IDataProtectionProvider dataProtectionProvider,
        IHostEnvironment environment,
        TimeProvider timeProvider
    )
    {
        keyPrefix = RedisKeyNamespace.Create(environment, "auth-ticket");
        redis = connection.GetDatabase();
        protector = dataProtectionProvider.CreateProtector(
            "ModulithFoundry.Api.Authentication.RedisTicketStore.v1"
        );
        this.timeProvider = timeProvider;
    }

    public Task<string> StoreAsync(AuthenticationTicket ticket) =>
        StoreAsync(ticket, CancellationToken.None);

    public async Task<string> StoreAsync(
        AuthenticationTicket ticket,
        CancellationToken cancellationToken
    )
    {
        string key = keyPrefix + RandomNumberGenerator.GetHexString(32);
        await WriteAsync(key, ticket, cancellationToken);
        return key;
    }

    public Task RenewAsync(
        string key,
        AuthenticationTicket ticket,
        CancellationToken cancellationToken
    ) => WriteAsync(RequireOwnedKey(key), ticket, cancellationToken);

    public Task RenewAsync(string key, AuthenticationTicket ticket) =>
        RenewAsync(key, ticket, CancellationToken.None);

    public async Task<AuthenticationTicket?> RetrieveAsync(
        string key,
        CancellationToken cancellationToken
    )
    {
        RedisValue value = await redis
            .StringGetAsync(RequireOwnedKey(key))
            .WaitAsync(cancellationToken);
        if (value.IsNull)
        {
            return null;
        }

        try
        {
            byte[] protectedTicket = (byte[])value!;
            return TicketSerializer.Default.Deserialize(protector.Unprotect(protectedTicket));
        }
        catch (CryptographicException)
        {
            await redis.KeyDeleteAsync(key).WaitAsync(cancellationToken);
            return null;
        }
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key) =>
        RetrieveAsync(key, CancellationToken.None);

    public async Task RemoveAsync(string key, CancellationToken cancellationToken) =>
        await redis.KeyDeleteAsync(RequireOwnedKey(key)).WaitAsync(cancellationToken);

    public Task RemoveAsync(string key) => RemoveAsync(key, CancellationToken.None);

    private async Task WriteAsync(
        string key,
        AuthenticationTicket ticket,
        CancellationToken cancellationToken
    )
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        TimeSpan lifetime = ticket.Properties.ExpiresUtc is { } expiresAt
            ? expiresAt - now
            : DefaultLifetime;
        if (lifetime <= TimeSpan.Zero)
        {
            await redis.KeyDeleteAsync(key).WaitAsync(cancellationToken);
            return;
        }

        byte[] serialized = TicketSerializer.Default.Serialize(ticket);
        byte[] protectedTicket = protector.Protect(serialized);
        await redis.StringSetAsync(key, protectedTicket, lifetime).WaitAsync(cancellationToken);
    }

    private string RequireOwnedKey(string key) =>
        key.StartsWith(keyPrefix, StringComparison.Ordinal)
            ? key
            : throw new ArgumentException("The authentication ticket key is invalid.", nameof(key));
}
