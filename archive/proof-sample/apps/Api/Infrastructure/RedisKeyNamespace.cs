namespace ModulithFoundry.Api.Infrastructure;

internal static class RedisKeyNamespace
{
    internal static string Create(IHostEnvironment environment, string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        string environmentName = string.Concat(
            environment.EnvironmentName.Select(character =>
                char.IsAsciiLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-'
            )
        );
        return $"modulith-foundry:{environmentName}:v1:{purpose}:";
    }
}
