namespace ModulithFoundry.Modules.Access.Organizations;

internal sealed class Organization
{
    private Organization()
    {
        Name = null!;
    }

    private Organization(Guid id, string name, OrganizationSlug slug, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Slug = slug;
        CreatedAt = createdAt;
    }

    internal Guid Id { get; private set; }

    internal string Name { get; private set; }

    internal OrganizationSlug Slug { get; private set; }

    internal DateTimeOffset CreatedAt { get; private set; }

    internal static Organization Create(
        Guid id,
        string name,
        string proposedSlug,
        DateTimeOffset createdAt
    )
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOrganizationNameException(
                "An organization name must contain between 1 and 200 characters."
            );
        }

        string canonicalName = name.Trim();
        if (canonicalName.Length is < 1 or > 200)
        {
            throw new InvalidOrganizationNameException(
                "An organization name must contain between 1 and 200 characters."
            );
        }

        return new Organization(
            id,
            canonicalName,
            OrganizationSlug.Create(proposedSlug),
            createdAt
        );
    }
}
