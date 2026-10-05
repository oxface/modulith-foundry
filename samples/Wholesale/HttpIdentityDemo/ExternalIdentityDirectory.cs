using ModulithFoundry.ActorIdentity;

namespace ModulithFoundry.Samples.Wholesale.HttpIdentityDemo;

// Immutable fixture directory. Durable Access will own users and external identities later.
public sealed class ExternalIdentityDirectory
{
    private readonly Dictionary<(string Issuer, string Subject), ActorId> _users;

    public ExternalIdentityDirectory(IConfiguration configuration)
    {
        _users = configuration
            .GetSection("IdentityDirectory")
            .GetChildren()
            .ToDictionary(
                entry => (Required(entry, "Issuer"), Required(entry, "Subject")),
                entry => new ActorId(Required(entry, "UserId"))
            );
    }

    public ActorId? Resolve(string issuer, string subject) =>
        _users.GetValueOrDefault((issuer, subject));

    private static string Required(IConfigurationSection entry, string name) =>
        !string.IsNullOrWhiteSpace(entry[name])
            ? entry[name]!
            : throw new InvalidOperationException(
                $"Configure {entry.Path}:{name} for the identity directory."
            );
}
