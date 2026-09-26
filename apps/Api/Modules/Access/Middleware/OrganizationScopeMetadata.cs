namespace ModulithFoundry.Api.Modules.Access.Middleware;

internal sealed class OrganizationScopeMetadata
{
    internal static OrganizationScopeMetadata Instance { get; } = new();

    private OrganizationScopeMetadata()
    {
    }
}
