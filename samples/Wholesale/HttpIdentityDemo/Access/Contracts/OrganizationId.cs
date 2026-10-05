namespace ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Access.Contracts;

public sealed record OrganizationId
{
    public OrganizationId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
}
