using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Sales.Customers;

internal sealed class Customer : IOrganizationOwned
{
    private Customer()
    {
        Code = null!;
        Name = null!;
    }

    private Customer(
        Guid id,
        Guid organizationId,
        string code,
        string name,
        DateTimeOffset createdAt
    )
    {
        Id = id;
        OrganizationId = organizationId;
        Code = code;
        Name = name;
        CreatedAt = createdAt;
    }

    internal Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal string Code { get; private set; }
    internal string Name { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }

    internal static Customer Create(
        Guid id,
        Guid organizationId,
        string code,
        string name,
        DateTimeOffset createdAt
    ) =>
        new(
            id,
            organizationId,
            CustomerInput.NormalizeCode(code),
            CustomerInput.NormalizeName(name),
            createdAt
        );
}
