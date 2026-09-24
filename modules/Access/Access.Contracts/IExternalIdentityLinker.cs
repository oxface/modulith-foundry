namespace ModulithFoundry.Modules.Access.Contracts;

public interface IExternalIdentityLinker
{
    Task<UserIdentityLink> LinkAsync(
        ExternalIdentity identity,
        CancellationToken cancellationToken = default);
}
