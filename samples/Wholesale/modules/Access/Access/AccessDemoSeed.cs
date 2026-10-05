using ModulithFoundry.Samples.Wholesale.Access.Persistence;

namespace ModulithFoundry.Samples.Wholesale.Access;

// Only the explicitly invoked setup mode uses these disposable demonstration rows.
public static class AccessDemoSeed
{
    public static void Stage(AccessDbContext database)
    {
        database.AddRange(
            new UserRow { Id = "application-alpha" },
            new UserRow { Id = "application-beta" }
        );
        database.AddRange(
            new ExternalIdentityRow
            {
                Issuer = "https://identity.test",
                Subject = "shared-subject",
                UserId = "application-alpha",
            },
            new ExternalIdentityRow
            {
                Issuer = "https://other-identity.test",
                Subject = "shared-subject",
                UserId = "application-beta",
            }
        );
        database.AddRange(
            new OrganizationRow { Id = "wholesale-alpha", Slug = "north-supply" },
            new OrganizationRow { Id = "wholesale-beta", Slug = "south-supply" }
        );
        database.AddRange(
            Membership("application-alpha", "wholesale-alpha"),
            Membership("application-alpha", "wholesale-beta"),
            Membership("application-beta", "wholesale-beta")
        );
    }

    private static MembershipRow Membership(string user, string organization) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = user,
            OrganizationId = organization,
            Status = MembershipStatus.Active,
        };
}
