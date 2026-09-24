namespace ModulithFoundry.Modules.Access.Persistence;

internal sealed class User
{
    private User()
    {
    }

    internal User(Guid id, string? email, string? displayName, DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    internal Guid Id { get; private set; }

    internal string? Email { get; private set; }

    internal string? DisplayName { get; private set; }

    internal DateTimeOffset CreatedAt { get; private set; }

    internal DateTimeOffset UpdatedAt { get; private set; }

    internal void UpdateProfile(string? email, string? displayName, DateTimeOffset updatedAt)
    {
        Email = email;
        DisplayName = displayName;
        UpdatedAt = updatedAt;
    }
}
