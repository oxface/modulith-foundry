namespace ModulithFoundry.Samples.Wholesale.Sales;

// Stage demonstration rows; the caller establishes tenancy and owns save/commit.
public static class SalesDemoSeed
{
    public static Guid AlphaCustomerId { get; } =
        Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static Guid BetaCustomerId { get; } = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static Guid AlphaAddressId { get; } = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static Guid BetaAddressId { get; } = Guid.Parse("20000000-0000-0000-0000-000000000002");

    public static void Stage(SalesDbContext database)
    {
        ArgumentNullException.ThrowIfNull(database);
        string organization = database.RequiredOrganizationKey;
        bool alpha = organization == "wholesale-alpha";
        if (!alpha && organization != "wholesale-beta")
            throw new ArgumentException(
                "The demonstration supports Alpha and Beta only.",
                nameof(database)
            );
        Guid customerId = alpha ? AlphaCustomerId : BetaCustomerId;
        database.Add(
            new CustomerRow
            {
                Id = customerId,
                OrganizationKey = organization,
                Code = "DEMO-CUSTOMER",
                DisplayName = alpha ? "Alpha Customer" : "Beta Customer",
            }
        );
        database.Add(
            new AddressRow
            {
                Id = alpha ? AlphaAddressId : BetaAddressId,
                CustomerId = customerId,
                OrganizationKey = organization,
                AddressLine = alpha ? "Alpha Street" : "Beta Street",
            }
        );
    }
}
