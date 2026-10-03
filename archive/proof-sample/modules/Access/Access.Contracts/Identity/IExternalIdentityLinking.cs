namespace ModulithFoundry.Modules.Access.Contracts;

public interface IExternalIdentityLinking
{
    Task<UserIdentityLink> LinkAsync(
        ExternalIdentity identity,
        CancellationToken cancellationToken = default
    );
}
