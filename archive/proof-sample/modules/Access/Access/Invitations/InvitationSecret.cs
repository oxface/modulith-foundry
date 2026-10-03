using System.Security.Cryptography;

namespace ModulithFoundry.Modules.Access.Invitations;

internal static class InvitationSecret
{
    internal static string Generate() =>
        Convert
            .ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    internal static byte[] Digest(string secret) =>
        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret));

    internal static bool Matches(string secret, byte[] expectedDigest) =>
        CryptographicOperations.FixedTimeEquals(Digest(secret), expectedDigest);
}
