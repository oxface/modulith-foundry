using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using ModulithFoundry.Api.Infrastructure;
using StackExchange.Redis;

namespace ModulithFoundry.Api.Modules.Access.Invitations;

internal sealed class RedisPendingInvitationAcceptanceStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
    private readonly string keyPrefix;
    private readonly IDataProtector protector;
    private readonly IDatabase redis;

    public RedisPendingInvitationAcceptanceStore(
        IConnectionMultiplexer connection,
        IDataProtectionProvider dataProtectionProvider,
        IHostEnvironment environment)
    {
        keyPrefix = RedisKeyNamespace.Create(environment, "pending-invitation-acceptance");
        redis = connection.GetDatabase();
        protector = dataProtectionProvider.CreateProtector(
            "ModulithFoundry.Api.Access.PendingInvitationAcceptance.v1");
    }

    internal async Task<string> CreateAsync(
        PendingInvitationAcceptance pendingAcceptance,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pendingAcceptance);

        string acceptanceHandle = RandomNumberGenerator.GetHexString(32);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(pendingAcceptance);
        byte[] protectedPayload = protector.Protect(payload);
        await redis.StringSetAsync(Key(acceptanceHandle), protectedPayload, Lifetime)
            .WaitAsync(cancellationToken);
        return acceptanceHandle;
    }

    internal async Task<PendingInvitationAcceptance?> RetrieveAsync(
        string acceptanceHandle,
        CancellationToken cancellationToken)
    {
        if (!IsValidHandle(acceptanceHandle))
        {
            return null;
        }

        RedisValue stored = await redis.StringGetAsync(Key(acceptanceHandle)).WaitAsync(cancellationToken);
        if (stored.IsNull)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PendingInvitationAcceptance>(
                protector.Unprotect((byte[])stored!));
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            await RemoveAsync(acceptanceHandle, cancellationToken);
            return null;
        }
    }

    internal async Task RemoveAsync(string acceptanceHandle, CancellationToken cancellationToken)
    {
        if (IsValidHandle(acceptanceHandle))
        {
            await redis.KeyDeleteAsync(Key(acceptanceHandle)).WaitAsync(cancellationToken);
        }
    }

    private RedisKey Key(string acceptanceHandle) => keyPrefix + acceptanceHandle;

    private static bool IsValidHandle(string? acceptanceHandle) =>
        acceptanceHandle is { Length: 32 } && acceptanceHandle.All(Uri.IsHexDigit);
}
