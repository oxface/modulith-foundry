namespace ModulithFoundry.Modules.Access.Contracts;

public abstract record CreateOrganizationResult
{
    private protected CreateOrganizationResult() { }

    public sealed record Created(OrganizationMembership Organization) : CreateOrganizationResult;

    public sealed record InvalidName(string Detail) : CreateOrganizationResult;

    public sealed record InvalidSlug(string Detail) : CreateOrganizationResult;

    public sealed record SlugUnavailable(string Slug) : CreateOrganizationResult;
}
